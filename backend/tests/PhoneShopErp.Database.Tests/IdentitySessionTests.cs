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
    public async Task RevokingASameTenantPeerSession_RequiresSessionRevoke()
    {
        var scenario = await CreateRevocationScenarioAsync("session-revoke-same-tenant");

        // The victim session is readable through the actor's own tenant-scoped
        // runtime path: the session read policy exposes every row in the selected
        // tenant. A denial therefore cannot be explained by RLS filtering.
        Assert.Equal(
            1,
            await ScalarInContextAsync<long>(
                ContextFrom(scenario.PeerResolved),
                $"SELECT count(*) FROM identity.session WHERE id = '{scenario.OwnerSession.Id}';"));

        var before = (await Database.QueryAsync(
            $"""
            SELECT revoked_at IS NULL, revoke_reason IS NULL, row_version
            FROM identity.session WHERE id = '{scenario.OwnerSession.Id}';
            """,
            "postgres"))[0];
        Assert.True((bool)before[0]!);
        Assert.True((bool)before[1]!);
        Assert.Equal(
            0,
            await CountSessionRevokedAuditAsync(scenario.Tenant.TenantId, scenario.OwnerSession.Id));

        PlatformException denial;
        using (TenantContextAccessor.Begin(ContextFrom(scenario.PeerResolved)))
        {
            denial = await Assert.ThrowsAsync<PlatformException>(
                () => fixture.Identity.RevokeSessionAsync(
                    scenario.OwnerSession.Id,
                    "peer denial",
                    CancellationToken.None));
        }

        // Forbidden (not NotFound) proves the denial comes from the authorization
        // check, because the row is visible and would otherwise be updatable.
        Assert.Equal(ErrorCodes.Forbidden, denial.Code);
        Assert.Equal(403, denial.StatusCode);

        var after = (await Database.QueryAsync(
            $"""
            SELECT revoked_at IS NULL, revoke_reason IS NULL, row_version
            FROM identity.session WHERE id = '{scenario.OwnerSession.Id}';
            """,
            "postgres"))[0];
        Assert.True((bool)after[0]!, "The denied call must not set revoked_at.");
        Assert.True((bool)after[1]!, "The denied call must not set revoke_reason.");
        Assert.Equal((int)before[2]!, (int)after[2]!);

        // No successful revocation was recorded for the denied attempt, and no
        // unrelated session was touched.
        Assert.Equal(
            0,
            await CountSessionRevokedAuditAsync(scenario.Tenant.TenantId, scenario.OwnerSession.Id));

        var peerRow = (await Database.QueryAsync(
            $"SELECT revoked_at IS NULL FROM identity.session WHERE id = '{scenario.PeerSession.Id}';",
            "postgres"))[0];
        Assert.True((bool)peerRow[0]!);

        // Independent verification through the real resolver: the victim session
        // is still active and resolves to the original session id.
        var victim = await fixture.Identity.ResolveAsync(scenario.OwnerPrincipal, CancellationToken.None);
        Assert.NotNull(victim);
        Assert.Equal(scenario.OwnerSession.Id, victim.SessionId);
    }

    [Fact]
    public async Task GrantingSessionRevoke_AllowsTheSameActorToRevokeThePeerSession()
    {
        var scenario = await CreateRevocationScenarioAsync("session-revoke-granted");

        // Baseline: the same actor, victim, tenant and code path are denied.
        using (TenantContextAccessor.Begin(ContextFrom(scenario.PeerResolved)))
        {
            var denied = await Assert.ThrowsAsync<PlatformException>(
                () => fixture.Identity.RevokeSessionAsync(
                    scenario.OwnerSession.Id,
                    "before grant",
                    CancellationToken.None));
            Assert.Equal(ErrorCodes.Forbidden, denied.Code);
        }

        // Grant the existing catalogue permission to the actor's existing role.
        // Nothing else about the actor, victim, tenant or session changes.
        await Database.ExecuteAsync(
            $"""
            INSERT INTO identity.role_permission (tenant_id, role_id, permission_code)
            VALUES ('{scenario.Tenant.TenantId}', '{scenario.Peer.RoleId}', 'session.revoke');
            """,
            "postgres");

        var granted = await fixture.Identity.ResolveAsync(scenario.PeerPrincipal, CancellationToken.None);
        Assert.NotNull(granted);
        Assert.Contains("session.revoke", granted.Permissions);

        using (TenantContextAccessor.Begin(ContextFrom(granted)))
        {
            await fixture.Identity.RevokeSessionAsync(
                scenario.OwnerSession.Id,
                "peer revoke approved",
                CancellationToken.None);
        }

        var row = (await Database.QueryAsync(
            $"""
            SELECT revoked_at IS NOT NULL, revoke_reason, row_version
            FROM identity.session WHERE id = '{scenario.OwnerSession.Id}';
            """,
            "postgres"))[0];
        Assert.True((bool)row[0]!);
        Assert.Equal("peer revoke approved", (string)row[1]!);
        Assert.True((int)row[2]! >= 2, "The revocation must pass through the row-version trigger.");

        Assert.Equal(
            1,
            await CountSessionRevokedAuditAsync(
                scenario.Tenant.TenantId,
                scenario.OwnerSession.Id,
                scenario.Peer.UserId));

        Assert.Null(await fixture.Identity.ResolveAsync(scenario.OwnerPrincipal, CancellationToken.None));
    }

    [Fact]
    public async Task SigningOutOwnSession_NeedsNoSessionRevokePermission()
    {
        var scenario = await CreateRevocationScenarioAsync("session-signout-own");

        Assert.DoesNotContain("session.revoke", scenario.PeerResolved.Permissions);

        using (TenantContextAccessor.Begin(ContextFrom(scenario.PeerResolved)))
        {
            await fixture.Identity.RevokeSessionAsync(
                scenario.PeerSession.Id,
                "sign out",
                CancellationToken.None);
        }

        Assert.Null(await fixture.Identity.ResolveAsync(scenario.PeerPrincipal, CancellationToken.None));

        // Self sign-out must not revoke anyone else's session.
        var ownerRow = (await Database.QueryAsync(
            $"SELECT revoked_at IS NULL FROM identity.session WHERE id = '{scenario.OwnerSession.Id}';",
            "postgres"))[0];
        Assert.True((bool)ownerRow[0]!);
    }

    [Fact]
    public async Task RevokingACrossTenantSession_IsNotFoundWithoutDisclosure()
    {
        var first = await fixture.CreateTenantAsync("session-revoke-cross-first");
        var second = await fixture.CreateTenantAsync("session-revoke-cross-second");
        var firstPrincipal = first.Principal("provider-ref-cross-first");
        var secondPrincipal = second.Principal("provider-ref-cross-second");

        await fixture.Identity.CreateSessionAsync(
            firstPrincipal,
            new CreateSessionRequest(first.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);
        var foreignSession = await fixture.Identity.CreateSessionAsync(
            secondPrincipal,
            new CreateSessionRequest(second.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        var resolved = await fixture.Identity.ResolveAsync(firstPrincipal, CancellationToken.None);
        Assert.NotNull(resolved);

        // The owner role holds session.revoke, so the 404 below is caused by
        // tenant scoping, not by a missing permission.
        Assert.Contains("session.revoke", resolved.Permissions);

        using (TenantContextAccessor.Begin(ContextFrom(resolved)))
        {
            var exception = await Assert.ThrowsAsync<PlatformException>(
                () => fixture.Identity.RevokeSessionAsync(
                    foreignSession.Id,
                    "cross tenant",
                    CancellationToken.None));
            Assert.Equal(ErrorCodes.NotFound, exception.Code);
            Assert.Equal(404, exception.StatusCode);
        }

        var foreignRow = (await Database.QueryAsync(
            $"SELECT revoked_at IS NULL FROM identity.session WHERE id = '{foreignSession.Id}';",
            "postgres"))[0];
        Assert.True((bool)foreignRow[0]!);
        Assert.NotNull(await fixture.Identity.ResolveAsync(secondPrincipal, CancellationToken.None));

        // The caller's own tenant remains unaffected.
        Assert.NotNull(await fixture.Identity.ResolveAsync(firstPrincipal, CancellationToken.None));
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

    /// <summary>
    /// One tenant with two members: the onboarded owner (victim) and a second
    /// membership whose role holds no <c>session.revoke</c> (actor). Both sessions
    /// are created through the real application path, and the actor's permission
    /// set is resolved from the database, not constructed in the test.
    /// </summary>
    private async Task<RevocationScenario> CreateRevocationScenarioAsync(string slug)
    {
        var tenant = await fixture.CreateTenantAsync(slug);
        var ownerPrincipal = tenant.Principal("provider-ref-" + slug + "-owner");
        var ownerSession = await fixture.Identity.CreateSessionAsync(
            ownerPrincipal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        var peer = await SeedPeerMembershipAsync(
            tenant.TenantId,
            "subject-" + slug + "-peer",
            "t02_limited",
            "tenant.view");

        var peerPrincipal = peer.Principal("provider-ref-" + slug + "-peer");
        var peerSession = await fixture.Identity.CreateSessionAsync(
            peerPrincipal,
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None);

        var peerResolved = await fixture.Identity.ResolveAsync(peerPrincipal, CancellationToken.None);
        Assert.NotNull(peerResolved);

        return new RevocationScenario(
            tenant,
            ownerPrincipal,
            ownerSession,
            peer,
            peerPrincipal,
            peerSession,
            peerResolved);
    }

    /// <summary>
    /// Seeds a second membership in an existing tenant. This is test setup only:
    /// the actor is then resolved and authenticated through the real adapters, so
    /// the permission check under test is never bypassed.
    /// </summary>
    private async Task<SeededPeer> SeedPeerMembershipAsync(
        Guid tenantId,
        string subject,
        string roleCode,
        params string[] permissionCodes)
    {
        var userId = await fixture.Identity.EnsureUserAsync(
            new AuthenticatedPrincipal(subject, null, "Peer " + subject, subject + "@example.test"),
            CancellationToken.None);

        var membershipId = fixture.Ids.NewId();
        var roleId = fixture.Ids.NewId();

        await Database.ExecuteAsync(
            $"""
            INSERT INTO identity.membership (id, tenant_id, user_id, status)
            VALUES ('{membershipId}', '{tenantId}', '{userId}', 'active');

            INSERT INTO identity.role (id, tenant_id, code, display_name)
            VALUES ('{roleId}', '{tenantId}', '{roleCode}', 'T-02 limited peer');

            INSERT INTO identity.role_assignment (
                id, tenant_id, membership_id, role_id, scope_type, scope_id)
            VALUES (
                '{fixture.Ids.NewId()}', '{tenantId}', '{membershipId}', '{roleId}',
                'tenant', NULL);
            """,
            "postgres");

        foreach (var permissionCode in permissionCodes)
        {
            await Database.ExecuteAsync(
                $"""
                INSERT INTO identity.role_permission (tenant_id, role_id, permission_code)
                VALUES ('{tenantId}', '{roleId}', '{permissionCode}');
                """,
                "postgres");
        }

        return new SeededPeer(userId, membershipId, roleId, subject);
    }

    private async Task<long> CountSessionRevokedAuditAsync(
        Guid tenantId,
        Guid sessionId,
        Guid? actorUserId = null)
    {
        var actorFilter = actorUserId is { } actor
            ? $" AND actor_user_id = '{actor}'"
            : string.Empty;

        return await Database.ScalarAsync<long>(
            $"""
            SELECT count(*)
            FROM platform.audit_event
            WHERE tenant_id = '{tenantId}'
              AND action = 'session.revoked'
              AND outcome = 'success'
              AND target_id = '{sessionId}'{actorFilter};
            """,
            "postgres");
    }

    private sealed record SeededPeer(Guid UserId, Guid MembershipId, Guid RoleId, string Subject)
    {
        public AuthenticatedPrincipal Principal(string sessionReference) =>
            new(Subject, sessionReference, "Peer " + Subject, Subject + "@example.test");
    }

    private sealed record RevocationScenario(
        SeededTenant Tenant,
        AuthenticatedPrincipal OwnerPrincipal,
        SessionSummary OwnerSession,
        SeededPeer Peer,
        AuthenticatedPrincipal PeerPrincipal,
        SessionSummary PeerSession,
        ResolvedSession PeerResolved);

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
