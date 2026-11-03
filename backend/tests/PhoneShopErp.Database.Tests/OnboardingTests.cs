using System.Text.Json;
using PhoneShopErp.Modules.Tenancy.Application;
using PhoneShopErp.Modules.Tenancy.Infrastructure;
using PhoneShopErp.Persistence;
using PhoneShopErp.Platform.Audit;
using PhoneShopErp.Platform.Errors;
using PhoneShopErp.Platform.Idempotency;
using Xunit;

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// Tenant onboarding: the only flow that starts pre-tenant and applies tenant
/// context inside one transaction. Everything below runs against PostgreSQL.
/// </summary>
[Collection(PostgresDatabaseGroup.Name)]
public sealed class OnboardingTests
{
    private readonly DatabaseFixture fixture;

    public OnboardingTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    private TestDatabase Database => fixture.Database;

    [Fact]
    public async Task Create_ProvisionsTenantMembershipOwnerRoleAndAuditAtomically()
    {
        var subject = "subject-onboard-full";
        var slug = UniqueSlug("onboard-full");

        var tenant = await fixture.Tenants.CreateAsync(
            Profile(subject),
            Request(slug),
            "onboarding-" + Guid.NewGuid().ToString("N"),
            CancellationToken.None);

        Assert.Equal(slug, tenant.Slug);
        Assert.Equal("active", tenant.Status);

        var membership = await Database.QueryAsync(
            $"""
            SELECT status, permission_version FROM identity.membership
            WHERE tenant_id = '{tenant.Id}' AND user_id = (SELECT id FROM identity.user WHERE subject = '{subject}');
            """,
            "postgres");
        Assert.Single(membership);
        Assert.Equal("active", (string)membership[0][0]!);
        Assert.Equal(1, (int)membership[0][1]!);

        var role = await Database.QueryAsync(
            $"SELECT code, is_system FROM identity.role WHERE tenant_id = '{tenant.Id}';",
            "postgres");
        Assert.Single(role);
        Assert.Equal("owner", (string)role[0][0]!);
        Assert.True((bool)role[0][1]!);

        var assignment = await Database.QueryAsync(
            $"""
            SELECT ra.scope_type, ra.scope_id, r.code
            FROM identity.role_assignment ra
            JOIN identity.role r ON r.id = ra.role_id
            WHERE ra.tenant_id = '{tenant.Id}';
            """,
            "postgres");
        Assert.Single(assignment);
        Assert.Equal("tenant", (string)assignment[0][0]!);
        Assert.Null(assignment[0][1]);
        Assert.Equal("owner", (string)assignment[0][2]!);

        var audit = await Database.QueryAsync(
            $"SELECT action FROM platform.audit_event WHERE tenant_id = '{tenant.Id}' ORDER BY action;",
            "postgres");
        Assert.Equal(
            ["role.assigned", "tenant.created"],
            audit.Select(row => (string)row[0]!).ToArray());
    }

    [Fact]
    public async Task Create_GrantsEveryTenantScopedPermissionAndNoPlatformPermission()
    {
        var slug = UniqueSlug("onboard-perms");
        var tenant = await fixture.Tenants.CreateAsync(
            Profile("subject-" + slug),
            Request(slug),
            "onboarding-" + Guid.NewGuid().ToString("N"),
            CancellationToken.None);

        var tenantScoped = await Database.ScalarAsync<long>(
            "SELECT count(*) FROM identity.permission WHERE scope = 'tenant' AND is_active;",
            "postgres");

        var granted = await Database.QueryAsync(
            $"""
            SELECT rp.permission_code, p.scope
            FROM identity.role_permission rp
            JOIN identity.permission p ON p.code = rp.permission_code
            WHERE rp.tenant_id = '{tenant.Id}';
            """,
            "postgres");

        Assert.Equal(tenantScoped, granted.Count);
        Assert.All(granted, row => Assert.Equal("tenant", (string)row[1]!));
        Assert.DoesNotContain(granted, row => (string)row[0]! is "tenant.create" or "tenant.suspend" or "security_event.view");
    }

    [Fact]
    public async Task Create_ReplaysTheSameResultForTheSameKeyAndPayload()
    {
        var subject = "subject-onboard-replay";
        var key = "onboarding-" + Guid.NewGuid().ToString("N");
        var request = Request(UniqueSlug("onboard-replay"));

        var first = await fixture.Tenants.CreateAsync(Profile(subject), request, key, CancellationToken.None);
        var second = await fixture.Tenants.CreateAsync(Profile(subject), request, key, CancellationToken.None);

        Assert.Equal(first, second);

        var tenants = await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM tenancy.tenant WHERE slug = '{request.Slug}';",
            "postgres");
        Assert.Equal(1, tenants);

        var memberships = await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM identity.membership WHERE tenant_id = '{first.Id}';",
            "postgres");
        Assert.Equal(1, memberships);

        var audit = await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM platform.audit_event WHERE tenant_id = '{first.Id}';",
            "postgres");
        Assert.Equal(2, audit);
    }

    [Fact]
    public async Task Create_RejectsTheSameKeyWithADifferentPayload()
    {
        var subject = "subject-onboard-conflict";
        var key = "onboarding-" + Guid.NewGuid().ToString("N");

        await fixture.Tenants.CreateAsync(
            Profile(subject),
            Request(UniqueSlug("onboard-conflict")),
            key,
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<PlatformException>(() => fixture.Tenants.CreateAsync(
            Profile(subject),
            Request(UniqueSlug("onboard-conflict-other")),
            key,
            CancellationToken.None));

        Assert.Equal(ErrorCodes.IdempotencyKeyReused, exception.Code);
        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsAKeyThatIsAlreadyInProgress()
    {
        var subject = "subject-onboard-in-progress";
        var key = "onboarding-" + Guid.NewGuid().ToString("N");
        var userId = await fixture.Identity.EnsureUserAsync(
            new PhoneShopErp.Modules.IdentityAccess.Application.AuthenticatedPrincipal(
                subject, "sid", "Owner", null),
            CancellationToken.None);

        var request = Request(UniqueSlug("onboard-progress"));
        var hash = Fingerprint(request);

        await Database.ExecuteAsync(
            $"""
            INSERT INTO platform.onboarding_request
                (requested_by_user_id, idempotency_key, request_hash, scope, status, created_tenant_id)
            VALUES ('{userId}', '{key}', '{hash}', 'tenant.create', 'in_progress', '{Guid.NewGuid()}');
            """,
            "postgres");

        var exception = await Assert.ThrowsAsync<PlatformException>(() => fixture.Tenants.CreateAsync(
            Profile(subject),
            request,
            key,
            CancellationToken.None));

        Assert.Equal(ErrorCodes.IdempotencyInProgress, exception.Code);
        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task Create_RollsBackEverythingWhenALaterStepFailsAndCanBeRetried()
    {
        var subject = "subject-onboard-rollback";
        var slug = UniqueSlug("onboard-rollback");
        var key = "onboarding-" + Guid.NewGuid().ToString("N");
        var request = Request(slug);

        var failing = new PostgresTenantService(
            fixture.PreTenantUnitOfWorkFactory,
            fixture.TenantUnitOfWorkFactory,
            new PostgresOnboardingRequestStore(),
            new FailingAuditStore(fixture.Audit, AuditActions.TenantCreated),
            fixture.Ids,
            fixture.Clock);

        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.CreateAsync(
            Profile(subject),
            request,
            key,
            CancellationToken.None));

        Assert.Equal(0, await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM tenancy.tenant WHERE slug = '{slug}';",
            "postgres"));
        Assert.Equal(0, await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM identity.user WHERE subject = '{subject}';",
            "postgres"));
        Assert.Equal(0, await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM platform.onboarding_request WHERE idempotency_key = '{key}';",
            "postgres"));

        // The failed attempt did not poison the key: a retry with the same key and
        // payload must succeed, because the reservation rolled back with the work.
        var retried = await fixture.Tenants.CreateAsync(Profile(subject), request, key, CancellationToken.None);
        Assert.Equal(slug, retried.Slug);
        Assert.Equal(1, await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM tenancy.tenant WHERE slug = '{slug}';",
            "postgres"));
    }

    [Fact]
    public async Task Create_RejectsAnUnauthenticatedSubjectWithoutTouchingTheDatabase()
    {
        var slug = UniqueSlug("onboard-anon");
        var before = await Database.ScalarAsync<long>("SELECT count(*) FROM identity.user;", "postgres");

        foreach (var subject in new[] { "", "   ", new string('s', 256) })
        {
            var exception = await Assert.ThrowsAsync<PlatformException>(() => fixture.Tenants.CreateAsync(
                new TenantOwnerProfile(subject, "Anonymous", null),
                Request(slug),
                "onboarding-" + Guid.NewGuid().ToString("N"),
                CancellationToken.None));

            Assert.Equal(ErrorCodes.Unauthenticated, exception.Code);
            Assert.Equal(401, exception.StatusCode);
        }

        Assert.Equal(before, await Database.ScalarAsync<long>("SELECT count(*) FROM identity.user;", "postgres"));
        Assert.Equal(0, await Database.ScalarAsync<long>(
            $"SELECT count(*) FROM tenancy.tenant WHERE slug = '{slug}';",
            "postgres"));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("UPPER-CASE")]
    [InlineData("-leading")]
    public async Task Create_RejectsAnInvalidSlug(string slug)
    {
        var exception = await Assert.ThrowsAsync<PlatformException>(() => fixture.Tenants.CreateAsync(
            Profile("subject-invalid-slug"),
            new CreateTenantRequest(slug, "Invalid", "en-GB", "Europe/London"),
            "onboarding-" + Guid.NewGuid().ToString("N"),
            CancellationToken.None));

        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
    }

    [Fact]
    public async Task Create_RequiresAnIdempotencyKeyOfBoundedLength()
    {
        foreach (var key in new[] { "", "short", new string('k', 129) })
        {
            var exception = await Assert.ThrowsAsync<PlatformException>(() => fixture.Tenants.CreateAsync(
                Profile("subject-bad-key"),
                Request(UniqueSlug("onboard-key")),
                key,
                CancellationToken.None));

            Assert.Equal(ErrorCodes.IdempotencyKeyRequired, exception.Code);
        }
    }

    [Fact]
    public async Task OnboardingKeysAreScopedPerUser_SoAnotherUserCannotReplayOrObserve()
    {
        var slug = UniqueSlug("onboard-per-user");
        var key = "onboarding-" + Guid.NewGuid().ToString("N");
        var request = new CreateTenantRequest(slug, "Shared Slug", "en-GB", "Europe/London");

        var first = await fixture.Tenants.CreateAsync(Profile("subject-key-owner"), request, key, CancellationToken.None);
        var second = await fixture.Tenants.CreateAsync(Profile("subject-key-other"), request, key, CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);

        var otherCannotSeeFirst = await fixture.Identity.GetMyMembershipsAsync(
            new PhoneShopErp.Modules.IdentityAccess.Application.AuthenticatedPrincipal(
                "subject-key-other", "sid", null, null),
            CancellationToken.None);

        Assert.DoesNotContain(otherCannotSeeFirst, membership => membership.TenantId == first.Id);
    }

    [Fact]
    public async Task OnboardingRequest_IsInvisibleToAnotherUserEvenByKey()
    {
        var slug = UniqueSlug("onboard-rls");
        var key = "onboarding-" + Guid.NewGuid().ToString("N");
        await fixture.Tenants.CreateAsync(Profile("subject-onboard-rls"), Request(slug), key, CancellationToken.None);

        var otherUserId = await fixture.Identity.EnsureUserAsync(
            new PhoneShopErp.Modules.IdentityAccess.Application.AuthenticatedPrincipal(
                "subject-onboard-rls-other", "sid", null, null),
            CancellationToken.None);

        var visible = await fixture.Database.QueryAsync(
            $"""
            SELECT count(*) FROM platform.onboarding_request
            WHERE idempotency_key = '{key}';
            """,
            "postgres");
        Assert.Equal(1L, visible[0][0]);

        // RLS: another user's pre-tenant context can never read that row.
        await using var connection = new Npgsql.NpgsqlConnection(Database.AppConnectionString);
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using (var set = new Npgsql.NpgsqlCommand("SELECT set_config('app.user_id', @id, true);", connection, transaction))
            {
                set.Parameters.AddWithValue("id", otherUserId.ToString());
                await set.ExecuteScalarAsync();
            }

            await using var command = new Npgsql.NpgsqlCommand(
                "SELECT count(*) FROM platform.onboarding_request;",
                connection,
                transaction);
            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);

            await transaction.RollbackAsync();
        }
    }

    private static TenantOwnerProfile Profile(string subject) =>
        new(subject, "Owner", subject + "@example.test");

    private static CreateTenantRequest Request(string slug) =>
        new(slug, "Tenant " + slug, "en-GB", "Europe/London");

    private static string Fingerprint(CreateTenantRequest request) =>
        RequestFingerprint.FromString(string.Join(
            '\n',
            request.Slug,
            request.DisplayName,
            request.DefaultLocale,
            request.DefaultTimezone));

    private static string UniqueSlug(string prefix) =>
        prefix + "-" + Guid.NewGuid().ToString("N")[..10];

    private sealed class FailingAuditStore : IAuditStore
    {
        private readonly IAuditStore inner;
        private readonly string action;

        public FailingAuditStore(IAuditStore inner, string action)
        {
            this.inner = inner;
            this.action = action;
        }

        public Task AppendAsync(
            IUnitOfWork unitOfWork,
            AuditEvent auditEvent,
            CancellationToken cancellationToken) =>
            auditEvent.Action == action
                ? throw new InvalidOperationException("Injected audit failure for test.")
                : inner.AppendAsync(unitOfWork, auditEvent, cancellationToken);

        public Task AppendSecurityEventAsync(
            IUnitOfWork unitOfWork,
            SecurityEvent securityEvent,
            CancellationToken cancellationToken) =>
            inner.AppendSecurityEventAsync(unitOfWork, securityEvent, cancellationToken);
    }
}
