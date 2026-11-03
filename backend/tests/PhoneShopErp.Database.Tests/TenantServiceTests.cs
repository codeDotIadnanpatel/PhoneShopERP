using PhoneShopErp.Modules.IdentityAccess.Application;
using PhoneShopErp.Modules.Tenancy.Application;
using PhoneShopErp.Platform.Errors;
using PhoneShopErp.Platform.Tenancy;
using Xunit;

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// Tenant service authorisation: context is mandatory, tenant mismatch is
/// rejected, suspension requires a platform grant, and every mutation is audited.
/// </summary>
[Collection(PostgresDatabaseGroup.Name)]
public sealed class TenantServiceTests
{
    private readonly DatabaseFixture fixture;

    public TenantServiceTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    private TestDatabase Database => fixture.Database;

    [Fact]
    public async Task Get_RequiresATenantContext()
    {
        var tenant = await fixture.CreateTenantAsync("tenant-get-no-context");

        var exception = await Assert.ThrowsAsync<PlatformException>(
            () => fixture.Tenants.GetAsync(tenant.TenantId, CancellationToken.None));

        Assert.Equal(ErrorCodes.TenantContextMissing, exception.Code);
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task Get_RejectsARequestedTenantThatIsNotTheSessionTenant()
    {
        var mine = await fixture.CreateTenantAsync("tenant-get-mine");
        var other = await fixture.CreateTenantAsync("tenant-get-other");

        using (TenantContextAccessor.Begin(Context(mine, ["tenant.view"])))
        {
            var exception = await Assert.ThrowsAsync<PlatformException>(
                () => fixture.Tenants.GetAsync(other.TenantId, CancellationToken.None));

            Assert.Equal(ErrorCodes.TenantMismatch, exception.Code);
            Assert.Equal(403, exception.StatusCode);
        }
    }

    [Fact]
    public async Task Get_RequiresTheTenantViewPermission()
    {
        var tenant = await fixture.CreateTenantAsync("tenant-get-forbidden");

        using (TenantContextAccessor.Begin(Context(tenant, [])))
        {
            var exception = await Assert.ThrowsAsync<PlatformException>(
                () => fixture.Tenants.GetAsync(tenant.TenantId, CancellationToken.None));

            Assert.Equal(ErrorCodes.Forbidden, exception.Code);
        }
    }

    [Fact]
    public async Task Get_ReturnsTheTenantUnderTheResolvedContext()
    {
        var tenant = await fixture.CreateTenantAsync("tenant-get-ok");

        using (TenantContextAccessor.Begin(Context(tenant, ["tenant.view"])))
        {
            var summary = await fixture.Tenants.GetAsync(tenant.TenantId, CancellationToken.None);

            Assert.Equal(tenant.TenantId, summary.Id);
            Assert.Equal("active", summary.Status);
            Assert.Equal(1, summary.RowVersion);
        }
    }

    [Fact]
    public async Task Suspend_RequiresAPlatformGrantNotATenantRole()
    {
        var tenant = await fixture.CreateTenantAsync("tenant-suspend-denied");

        // The owner role grants every tenant permission, including nothing that
        // can suspend a tenant. Without the platform grant the call fails closed.
        using (TenantContextAccessor.Begin(Context(tenant, ["tenant.configure"], ["tenant.view"])))
        {
            var exception = await Assert.ThrowsAsync<PlatformException>(
                () => fixture.Tenants.SuspendAsync(tenant.TenantId, "billing review", CancellationToken.None));

            Assert.Equal(ErrorCodes.Forbidden, exception.Code);
        }
    }

    [Fact]
    public async Task SuspendAndReactivate_AuditAndBumpTheRowVersion()
    {
        var tenant = await fixture.CreateTenantAsync("tenant-suspend-ok");

        using (TenantContextAccessor.Begin(Context(tenant, [], ["tenant.suspend"])))
        {
            var suspended = await fixture.Tenants.SuspendAsync(
                tenant.TenantId,
                "billing review",
                CancellationToken.None);

            Assert.Equal("suspended", suspended.Status);
            Assert.Equal(2, suspended.RowVersion);
        }

        var audits = await Database.QueryAsync(
            $"""
            SELECT action, reason FROM platform.audit_event
            WHERE tenant_id = '{tenant.TenantId}' AND action LIKE 'tenant.%'
            ORDER BY action;
            """,
            "postgres");

        Assert.Contains(audits, row => (string)row[0]! == "tenant.suspended" && (string?)row[1] == "billing review");

        using (TenantContextAccessor.Begin(Context(tenant, [], ["tenant.suspend"])))
        {
            var reactivated = await fixture.Tenants.ReactivateAsync(
                tenant.TenantId,
                "payment received",
                CancellationToken.None);

            Assert.Equal("active", reactivated.Status);
            Assert.Equal(3, reactivated.RowVersion);
        }
    }

    [Fact]
    public async Task SuspendedTenant_CannotStartANewSession()
    {
        var tenant = await fixture.CreateTenantAsync("tenant-suspended-session");

        using (TenantContextAccessor.Begin(Context(tenant, [], ["tenant.suspend"])))
        {
            await fixture.Tenants.SuspendAsync(tenant.TenantId, "billing review", CancellationToken.None);
        }

        var exception = await Assert.ThrowsAsync<PlatformException>(() => fixture.Identity.CreateSessionAsync(
            tenant.Principal("provider-ref-suspended"),
            new CreateSessionRequest(tenant.TenantId, null, null),
            new SessionRequestMetadata(null, null, null, null),
            CancellationToken.None));

        Assert.Equal(ErrorCodes.Forbidden, exception.Code);
        Assert.Equal(403, exception.StatusCode);
    }

    [Fact]
    public async Task Suspend_RejectsAReasonOutsideTheBoundedRange()
    {
        var tenant = await fixture.CreateTenantAsync("tenant-suspend-reason");

        using (TenantContextAccessor.Begin(Context(tenant, [], ["tenant.suspend"])))
        {
            var exception = await Assert.ThrowsAsync<PlatformException>(
                () => fixture.Tenants.SuspendAsync(tenant.TenantId, "no", CancellationToken.None));

            Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
        }
    }

    private static TenantContext Context(
        SeededTenant tenant,
        IEnumerable<string> tenantPermissions,
        IEnumerable<string>? platformPermissions = null) =>
        TenantContext.Create(
            tenant.TenantId,
            tenant.UserId,
            Guid.NewGuid(),
            tenant.MembershipId,
            1,
            tenantWideScope: true,
            permissions: tenantPermissions,
            platformPermissions: platformPermissions ?? []);
}
