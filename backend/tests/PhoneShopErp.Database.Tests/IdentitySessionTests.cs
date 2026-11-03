using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PhoneShopErp.Modules.IdentityAccess.Application;
using PhoneShopErp.Modules.IdentityAccess.Infrastructure;
using PhoneShopErp.Platform.Errors;
using PhoneShopErp.Platform.Idempotency;
using PhoneShopErp.Platform.Tenancy;
using Xunit;

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// Identity and session behaviour against PostgreSQL: user creation, membership
/// picker, session issuance, server-side permission resolution, revocation and
/// expiry. Nothing here trusts a token claim for tenant, role or permission.
/// </summary>
[Collection(PostgresDatabaseGroup.Name)]
public sealed class IdentitySessionTests
{
    private readonly DatabaseFixture fixture;

    public IdentitySessionTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    private TestDatabase Database => fixture.Database;

    [Fact]
    public async Task EnsureUser_IsIdempotentForTheSameSubject()
    {
        var principal = new AuthenticatedPrincipal("subject-ensure-user", "sid", "First Name", "first@example.test");

        var first = await fixture.Identity.EnsureUserAsync(principal, CancellationToken.None);
        var second = await fixture.Identity.EnsureUserAsync(
            new AuthenticatedPrincipal("subject-ensure-user", "sid-2", "Another Name", "other@example.test"),
            CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal(1, await Database.ScalarAsync<long>(
            "SELECT count(*) FROM identity.user WHERE subject = 'subject-ensure-user';",
            "postgres"));
    }

    [Fact]
    public async Task ResolveSession_UsesServerSidePermissionsAndNoTokenClaims()
    {
        var tenant = await fixture.CreateTenantAsync("session-permissions");
        var principal = tenant.Principal("provider-ref-permissions");

        var session = await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(tenant.TenantId, "Desk", "windows"),
            new SessionRequestMetadata("Desk", "windows", "test-agent", "10.0.0.9"),
            CancellationToken.None);

        var resolved = await fixture.Identity.ResolveAsync(principal, CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(session.Id, resolved.SessionId);
        Assert.Equal(tenant.TenantId, resolved.TenantId);
        Assert.Equal(tenant.UserId, resolved.UserId);
        Assert.Equal(tenant.MembershipId, resolved.MembershipId);
        Assert.True(resolved.TenantWideScope);
        Assert.Contains("tenant.view", resolved.Permissions);
        Assert.Empty(resolved.PlatformPermissions);
        Assert.Equal(
            await Database.ScalarAsync<long>(
                "SELECT count(*) FROM identity.permission WHERE scope = 'tenant' AND is_active;",
                "postgres"),
            resolved.Permissions.Count);
        Assert.InRange(session.IdleExpiresAt, DateTimeOffset.UtcNow.AddMinutes(28), DateTimeOffset.UtcNow.AddMinutes(32));
        Assert.InRange(session.AbsoluteExpiresAt, DateTimeOffset.UtcNow.AddHours(11.9), DateTimeOffset.UtcNow.AddHours(12.1));
    }

    [Fact]
    public async Task Session_StoresOnlyAnOpaqueBindingFingerprint()
    {
        var tenant = await fixture.CreateTenantAsync("session-fingerprint");
        var principal = tenant.Principal("provider-ref-fingerprint");

        await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(tenant.TenantId, "Desk", "windows"),
            new SessionRequestMetadata("Desk", "windows", "secret-agent-value", "203.0.113.9"),
            CancellationToken.None);

        var row = await Database.QueryAsync(
            $"""
            SELECT fingerprint_hash, provider_session_reference
            FROM identity.session WHERE tenant_id = '{tenant.TenantId}';
            """,
            "postgres");

        Assert.Single(row);
        var fingerprint = (string)row[0][0]!;
        Assert.Equal(64, fingerprint.Length);
        Assert.Matches("^[0-9a-f]{64}$", fingerprint);
        Assert.DoesNotContain("secret-agent-value", fingerprint, StringComparison.Ordinal);
        Assert.DoesNotContain("203.0.113.9", fingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateSession_RejectsATenantTheUserDoesNotBelongTo()
    {
        var tenant = await fixture.CreateTenantAsync("session-no-member");
        var principal = tenant.Principal("provider-ref-no-member");

        await fixture.Identity.EnsureUserAsync(principal, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<PlatformException>(() => fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(Guid.NewGuid(), null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None));

        Assert.Equal(ErrorCodes.NotFound, exception.Code);
        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task CreateSession_RequiresAProviderSessionReferenceAndATenant()
    {
        var tenant = await fixture.CreateTenantAsync("session-validation");

        var noReference = await Assert.ThrowsAsync<PlatformException>(() => fixture.Identity.CreateSessionAsync(
            new AuthenticatedPrincipal(tenant.Subject, null, null, null),
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None));
        Assert.Equal(ErrorCodes.Unauthenticated, noReference.Code);

        var noTenant = await Assert.ThrowsAsync<PlatformException>(() => fixture.Identity.CreateSessionAsync(
            tenant.Principal("provider-ref-validation"),
            new CreateSessionRequest(Guid.Empty, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None));
        Assert.Equal(ErrorCodes.ValidationFailed, noTenant.Code);
    }

    [Fact]
    public async Task CreateSession_IsIdempotentForTheSameReferenceAndTenant()
    {
        var tenant = await fixture.CreateTenantAsync("session-idempotent");
        var principal = tenant.Principal("provider-ref-idempotent");

        var first = await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        var second = await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM identity.session WHERE tenant_id = '{tenant.TenantId}';",
            "postgres"));
    }

    [Fact]
    public async Task SwitchingTenant_RevokesTheOldSessionAndAuditsTheSwitch()
    {
        var first = await fixture.CreateTenantAsync("switch-a");
        var second = await fixture.CreateTenantAsync("switch-b", first.Subject);
        var principal = first.Principal("provider-ref-switch");

        var sessionA = await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(first.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        var sessionB = await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(second.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        Assert.NotEqual(sessionA.Id, sessionB.Id);

        var revoked = await Database.QueryAsync(
            $"SELECT revoked_at, revoke_reason FROM identity.session WHERE id = '{sessionA.Id}';",
            "postgres");
        Assert.NotNull(revoked[0][0]);
        Assert.Equal("tenant_switch", (string)revoked[0][1]!);

        var switched = await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM platform.audit_event WHERE tenant_id = '{first.TenantId}' AND action = 'session.tenant_switched';",
            "postgres");
        Assert.Equal(1, switched);

        var resolved = await fixture.Identity.ResolveAsync(principal, CancellationToken.None);
        Assert.NotNull(resolved);
        Assert.Equal(second.TenantId, resolved.TenantId);
    }

    [Fact]
    public async Task Resolve_ReturnsNullForARevokedSession()
    {
        var tenant = await fixture.CreateTenantAsync("session-revoked");
        var principal = tenant.Principal("provider-ref-revoked");
        var session = await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        var resolved = await fixture.Identity.ResolveAsync(principal, CancellationToken.None);
        Assert.NotNull(resolved);

        using (TenantContextAccessor.Begin(ContextFrom(resolved)))
        {
            await fixture.Identity.RevokeSessionAsync(session.Id, "sign out", CancellationToken.None);
        }

        Assert.Null(await fixture.Identity.ResolveAsync(principal, CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_ReturnsNullForAnExpiredSession()
    {
        var tenant = await fixture.CreateTenantAsync("session-expired");
        var principal = tenant.Principal("provider-ref-expired");

        await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        Assert.NotNull(await fixture.Identity.ResolveAsync(principal, CancellationToken.None));

        await Database.ExecuteAsync(
            $"""
            UPDATE identity.session
            SET issued_at = now() - interval '1 day',
                idle_expires_at = now() - interval '1 hour',
                absolute_expires_at = now() - interval '30 minutes'
            WHERE tenant_id = '{tenant.TenantId}';
            """,
            "postgres");

        Assert.Null(await fixture.Identity.ResolveAsync(principal, CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_ReturnsNullWhenMembershipOrUserIsNoLongerActive()
    {
        var tenant = await fixture.CreateTenantAsync("session-inactive");
        var principal = tenant.Principal("provider-ref-inactive");

        await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        await Database.ExecuteAsync(
            $"UPDATE identity.membership SET status = 'suspended' WHERE id = '{tenant.MembershipId}';",
            "postgres");
        Assert.Null(await fixture.Identity.ResolveAsync(principal, CancellationToken.None));

        await Database.ExecuteAsync(
            $"UPDATE identity.membership SET status = 'active' WHERE id = '{tenant.MembershipId}';",
            "postgres");
        await Database.ExecuteAsync(
            $"UPDATE identity.user SET status = 'deactivated' WHERE id = '{tenant.UserId}';",
            "postgres");
        Assert.Null(await fixture.Identity.ResolveAsync(principal, CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_ReturnsNullForAnotherUsersProviderReference()
    {
        var tenant = await fixture.CreateTenantAsync("session-cross-user");
        var principal = tenant.Principal("shared-provider-reference");

        await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        var other = await fixture.CreateTenantAsync("session-cross-user-other");
        var impersonating = new AuthenticatedPrincipal(other.Subject, "shared-provider-reference", null, null);

        Assert.Null(await fixture.Identity.ResolveAsync(impersonating, CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_ReturnsNullWithoutAProviderSessionReference()
    {
        var tenant = await fixture.CreateTenantAsync("session-no-sid");

        Assert.Null(await fixture.Identity.ResolveAsync(
            new AuthenticatedPrincipal(tenant.Subject, null, "Owner", null),
            CancellationToken.None));
    }

    [Fact]
    public async Task RevokingAnotherUsersSession_RequiresSessionRevoke()
    {
        var tenant = await fixture.CreateTenantAsync("session-revoke-other");
        var ownerPrincipal = tenant.Principal("provider-ref-owner");
        var ownerSession = await fixture.Identity.CreateSessionAsync(
            ownerPrincipal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        var other = await fixture.CreateTenantAsync("session-revoke-victim");
        var victimSession = await fixture.Identity.CreateSessionAsync(
            other.Principal("provider-ref-victim"),
            new CreateSessionRequest(other.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        // A session in the same tenant without session.revoke must not be able to
        // revoke a peer session. Simulate with a permission-less context.
        var denied = TenantContext.Create(
            tenant.TenantId,
            tenant.UserId,
            ownerSession.Id,
            tenant.MembershipId,
            permissionVersion: 1,
            tenantWideScope: true);

        using (TenantContextAccessor.Begin(denied))
        {
            var exception = await Assert.ThrowsAsync<PlatformException>(
                () => fixture.Identity.RevokeSessionAsync(victimSession.Id, "not allowed", CancellationToken.None));
            Assert.Equal(ErrorCodes.Forbidden, exception.Code);

            // Signing out of one's own session needs no permission at all.
            await fixture.Identity.RevokeSessionAsync(ownerSession.Id, "sign out", CancellationToken.None);
        }

        Assert.Null(await fixture.Identity.ResolveAsync(ownerPrincipal, CancellationToken.None));
    }

    [Fact]
    public async Task RevokeSession_RequiresABoundedReason()
    {
        var tenant = await fixture.CreateTenantAsync("session-revoke-reason");
        var principal = tenant.Principal("provider-ref-reason");
        var session = await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        var resolved = await fixture.Identity.ResolveAsync(principal, CancellationToken.None);

        using (TenantContextAccessor.Begin(ContextFrom(resolved!)))
        {
            var exception = await Assert.ThrowsAsync<PlatformException>(
                () => fixture.Identity.RevokeSessionAsync(session.Id, "  ", CancellationToken.None));
            Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
        }
    }

    [Fact]
    public async Task APlatformPermissionInsertedIntoATenantRole_IsNeverResolved()
    {
        var tenant = await fixture.CreateTenantAsync("session-escalation");
        var principal = tenant.Principal("provider-ref-escalation");

        await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        var ownerRoleId = await Database.ScalarAsync<Guid>(
            $"SELECT id FROM identity.role WHERE tenant_id = '{tenant.TenantId}' AND code = 'owner';",
            "postgres");

        // A deliberate data defect: a platform-scoped permission granted to a
        // tenant role. The resolver must still not surface it in either set.
        await Database.ExecuteAsync(
            $"""
            INSERT INTO identity.role_permission (tenant_id, role_id, permission_code)
            VALUES ('{tenant.TenantId}', '{ownerRoleId}', 'tenant.suspend');
            """,
            "postgres");

        var resolved = await fixture.Identity.ResolveAsync(principal, CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.DoesNotContain("tenant.suspend", resolved.Permissions);
        Assert.DoesNotContain("tenant.suspend", resolved.PlatformPermissions);
    }

    [Fact]
    public async Task PlatformGrants_ResolveSeparatelyFromTenantPermissions()
    {
        var tenant = await fixture.CreateTenantAsync("session-platform-grant");
        var principal = tenant.Principal("provider-ref-platform");

        await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        await Database.ExecuteAsync(
            $"""
            INSERT INTO identity.platform_grant (id, user_id, permission_code)
            VALUES ('{Guid.NewGuid()}', '{tenant.UserId}', 'tenant.suspend');
            """,
            "postgres");

        var resolved = await fixture.Identity.ResolveAsync(principal, CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Contains("tenant.suspend", resolved.PlatformPermissions);
        Assert.DoesNotContain("tenant.suspend", resolved.Permissions);
    }

    [Fact]
    public async Task GetMySessions_IsTenantScopedAndExcludesRevokedRows()
    {
        var tenant = await fixture.CreateTenantAsync("session-list");
        var principal = tenant.Principal("provider-ref-list");
        var session = await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        var resolved = await fixture.Identity.ResolveAsync(principal, CancellationToken.None);

        using (TenantContextAccessor.Begin(ContextFrom(resolved!)))
        {
            var sessions = await fixture.Identity.GetMySessionsAsync(CancellationToken.None);
            Assert.Single(sessions);
            Assert.Equal(session.Id, sessions[0].Id);

            await fixture.Identity.RevokeSessionAsync(session.Id, "sign out", CancellationToken.None);

            Assert.Empty(await fixture.Identity.GetMySessionsAsync(CancellationToken.None));
        }

        using (TenantContextAccessor.Begin(ContextFrom(resolved!)))
        {
            var exception = await Assert.ThrowsAsync<PlatformException>(
                () => fixture.Identity.RevokeSessionAsync(session.Id, "again", CancellationToken.None));
            Assert.Equal(ErrorCodes.NotFound, exception.Code);
        }
    }

    [Fact]
    public async Task GetMyMemberships_ReturnsOnlyTheCallersMemberships()
    {
        var first = await fixture.CreateTenantAsync("membership-list-a");
        var second = await fixture.CreateTenantAsync("membership-list-b", first.Subject);
        var stranger = await fixture.CreateTenantAsync("membership-list-stranger");

        var memberships = await fixture.Identity.GetMyMembershipsAsync(
            new AuthenticatedPrincipal(first.Subject, "sid", null, null),
            CancellationToken.None);

        Assert.Equal(2, memberships.Count);
        Assert.Contains(memberships, membership => membership.TenantId == first.TenantId);
        Assert.Contains(memberships, membership => membership.TenantId == second.TenantId);
        Assert.DoesNotContain(memberships, membership => membership.TenantId == stranger.TenantId);
    }

    [Fact]
    public async Task UnauthenticatedAndUnknownSubjects_AreRejected()
    {
        await Assert.ThrowsAsync<PlatformException>(() => fixture.Identity.EnsureUserAsync(
            new AuthenticatedPrincipal(string.Empty, "sid", null, null),
            CancellationToken.None));

        Assert.Empty(await fixture.Identity.GetMyMembershipsAsync(
            new AuthenticatedPrincipal("subject-never-seen", "sid", null, null),
            CancellationToken.None));
    }

    /// <summary>
    /// Steps 1-10 of the identity flow, minus the external identity provider:
    /// validated token subject, ERP user, membership picker, session, effective
    /// permissions, trusted tenant context, tenant data, revocation, denial.
    /// </summary>
    [Fact]
    public async Task EndToEnd_FromValidatedTokenToRevokedSession()
    {
        var tenant = await fixture.CreateTenantAsync("e2e-flow");
        var subject = tenant.Subject;

        using var rsa = RSA.Create(2048);
        var configuration = new OpenIdConnectConfiguration { Issuer = "https://identity.example.test" };
        configuration.SigningKeys.Add(new RsaSecurityKey(rsa) { KeyId = "e2e" });

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = configuration.Issuer,
            Audience = "phoneshoperp-api",
            IssuedAt = DateTime.UtcNow.AddMinutes(-1),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = subject,
                ["sid"] = "e2e-provider-session",
                ["name"] = "E2E Owner",
                ["tenant_id"] = Guid.NewGuid().ToString(),
                ["permissions"] = "tenant.suspend",
            },
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(rsa) { KeyId = "e2e" },
                SecurityAlgorithms.RsaSha256),
        });

        var validator = new OidcSubjectTokenValidator(configuration, "phoneshoperp-api");
        var validated = await validator.ValidateAsync(token, CancellationToken.None);
        Assert.True(validated.IsValid);
        var principal = validated.Principal!;

        var userId = await fixture.Identity.EnsureUserAsync(principal, CancellationToken.None);
        Assert.Equal(tenant.UserId, userId);

        var memberships = await fixture.Identity.GetMyMembershipsAsync(principal, CancellationToken.None);
        Assert.Single(memberships);
        Assert.Equal(tenant.TenantId, memberships[0].TenantId);

        var session = await fixture.Identity.CreateSessionAsync(
            principal,
            new CreateSessionRequest(memberships[0].TenantId, "E2E desk", "windows"),
            new SessionRequestMetadata("E2E desk", "windows", "e2e", "198.51.100.7"),
            CancellationToken.None);

        var resolved = await fixture.Identity.ResolveAsync(principal, CancellationToken.None);
        Assert.NotNull(resolved);
        Assert.Contains("tenant.view", resolved.Permissions);
        Assert.DoesNotContain("tenant.suspend", resolved.Permissions);
        Assert.DoesNotContain("tenant.suspend", resolved.PlatformPermissions);

        var context = ContextFrom(resolved);

        // Tenant data is readable only under the trusted context, as erp_app.
        using (TenantContextAccessor.Begin(context))
        {
            var visible = await ScalarInContextAsync<long>(
                context,
                "SELECT count(*) FROM tenancy.tenant;");
            Assert.Equal(1, visible);
        }

        using (TenantContextAccessor.Begin(context))
        {
            await fixture.Identity.RevokeSessionAsync(session.Id, "end of test", CancellationToken.None);
        }

        Assert.Null(await fixture.Identity.ResolveAsync(principal, CancellationToken.None));

        // The revoked session cannot read tenant data either: no context can be
        // derived, and the raw session row is gone from the valid set.
        Assert.Null(await fixture.Identity.ResolveAsync(principal, CancellationToken.None));
    }

    private static TenantContext ContextFrom(ResolvedSession resolved) =>
        TenantContext.Create(
            resolved.TenantId,
            resolved.UserId,
            resolved.SessionId,
            resolved.MembershipId,
            resolved.PermissionVersion,
            resolved.TenantWideScope,
            resolved.Permissions,
            resolved.PlatformPermissions,
            resolved.BranchIds,
            resolved.LegalEntityIds,
            resolved.StockLocationIds);

    private async Task<T?> ScalarInContextAsync<T>(TenantContext context, string sql)
    {
        await using var connection = new NpgsqlConnection(Database.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        foreach (var (name, settingValue) in new Dictionary<string, string>
                 {
                     ["app.tenant_id"] = context.TenantId.ToString(),
                     ["app.user_id"] = context.UserId.ToString(),
                     ["app.session_id"] = context.SessionId.ToString(),
                 })
        {
            await using var set = new NpgsqlCommand("SELECT set_config(@name, @value, true);", connection, transaction);
            set.Parameters.AddWithValue("name", name);
            set.Parameters.AddWithValue("value", settingValue);
            await set.ExecuteScalarAsync();
        }

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var result = await command.ExecuteScalarAsync();
        await transaction.RollbackAsync();

        return result is null or DBNull ? default : (T)result;
    }
}
