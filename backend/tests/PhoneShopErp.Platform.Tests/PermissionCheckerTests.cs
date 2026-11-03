using PhoneShopErp.Platform.Errors;
using PhoneShopErp.Platform.Security;
using PhoneShopErp.Platform.Tenancy;
using Xunit;

namespace PhoneShopErp.Platform.Tests;

public sealed class PermissionCheckerTests
{
    private static TenantContext Context(
        IEnumerable<string>? permissions = null,
        IEnumerable<string>? platformPermissions = null,
        bool tenantWide = false,
        IEnumerable<Guid>? branchIds = null,
        IEnumerable<Guid>? legalEntityIds = null,
        IEnumerable<Guid>? stockLocationIds = null) =>
        TenantContext.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            tenantWide,
            permissions,
            platformPermissions,
            branchIds,
            legalEntityIds,
            stockLocationIds);

    [Fact]
    public void Demand_DeniesByDefault()
    {
        var exception = Assert.Throws<PlatformException>(
            () => PermissionChecker.Demand(Context(), "tenant.view"));

        Assert.Equal(ErrorCodes.Forbidden, exception.Code);
        Assert.Equal(403, exception.StatusCode);
    }

    [Fact]
    public void Demand_AllowsOnlyTheExactPermissionCode()
    {
        var context = Context(["tenant.view"]);

        PermissionChecker.Demand(context, "tenant.view");
        Assert.Throws<PlatformException>(() => PermissionChecker.Demand(context, "tenant.view.all"));
        Assert.Throws<PlatformException>(() => PermissionChecker.Demand(context, "TENANT.VIEW"));
    }

    [Fact]
    public void TenantPermission_NeverGrantsPlatformPermission()
    {
        var context = Context(["tenant.suspend"], ["security_event.view"]);

        Assert.True(PermissionChecker.Has(context, "tenant.suspend"));
        Assert.False(PermissionChecker.HasPlatform(context, "tenant.suspend"));
        Assert.True(PermissionChecker.HasPlatform(context, "security_event.view"));
        Assert.False(PermissionChecker.Has(context, "security_event.view"));
    }

    [Fact]
    public void DemandPlatform_DeniesWithoutAnExplicitPlatformGrant()
    {
        var exception = Assert.Throws<PlatformException>(
            () => PermissionChecker.DemandPlatform(Context(), "tenant.suspend"));

        Assert.Equal(ErrorCodes.Forbidden, exception.Code);
    }

    [Fact]
    public void BranchScope_IsDenyByDefaultAndRequiresExplicitGrant()
    {
        var granted = Guid.NewGuid();
        var other = Guid.NewGuid();
        var context = Context(["branch.view"], branchIds: [granted]);

        PermissionChecker.DemandBranch(context, granted);
        Assert.Throws<PlatformException>(() => PermissionChecker.DemandBranch(context, other));
        Assert.Throws<PlatformException>(() => PermissionChecker.DemandBranch(context, Guid.Empty));
    }

    [Fact]
    public void TenantWideScope_AllowsEveryBranchEntityAndLocation()
    {
        var context = Context(tenantWide: true);

        PermissionChecker.DemandBranch(context, Guid.NewGuid());
        PermissionChecker.DemandLegalEntity(context, Guid.NewGuid());
        PermissionChecker.DemandStockLocation(context, Guid.NewGuid());
    }

    [Fact]
    public void LegalEntityAndStockLocationScope_AreSeparate()
    {
        var entityId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var context = Context(
            legalEntityIds: [entityId],
            stockLocationIds: [locationId]);

        PermissionChecker.DemandLegalEntity(context, entityId);
        PermissionChecker.DemandStockLocation(context, locationId);
        Assert.Throws<PlatformException>(() => PermissionChecker.DemandLegalEntity(context, locationId));
        Assert.Throws<PlatformException>(() => PermissionChecker.DemandStockLocation(context, entityId));
    }
}
