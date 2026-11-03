using PhoneShopErp.Platform.Tenancy;
using Xunit;

namespace PhoneShopErp.Platform.Tests;

public sealed class TenantContextTests
{
    private static TenantContext CreateContext(
        IEnumerable<string>? permissions = null,
        IEnumerable<Guid>? branchIds = null) =>
        TenantContext.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            permissions: permissions,
            branchIds: branchIds);

    [Fact]
    public void Create_RejectsEmptyIdentifiers()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => TenantContext.Create(
            Guid.Empty, userId, Guid.NewGuid(), Guid.NewGuid(), 1));
        Assert.Throws<ArgumentException>(() => TenantContext.Create(
            tenantId, Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 1));
        Assert.Throws<ArgumentException>(() => TenantContext.Create(
            tenantId, userId, Guid.Empty, Guid.NewGuid(), 1));
        Assert.Throws<ArgumentException>(() => TenantContext.Create(
            tenantId, userId, Guid.NewGuid(), Guid.Empty, 1));
    }

    [Fact]
    public void Create_RejectsNonPositivePermissionVersion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TenantContext.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0));
    }

    [Fact]
    public void Permissions_AreCopiedAndCannotBeWidenedByTheCaller()
    {
        var permissions = new List<string> { "tenant.view" };
        var context = CreateContext(permissions);

        permissions.Add("tenant.suspend");

        Assert.Single(context.Permissions);
        Assert.Contains("tenant.view", context.Permissions);
        Assert.DoesNotContain("tenant.suspend", context.Permissions);
    }

    [Fact]
    public void BranchScope_IsCopiedAndCannotBeWidenedByTheCaller()
    {
        var branchIds = new List<Guid> { Guid.NewGuid() };
        var context = CreateContext(branchIds: branchIds);

        branchIds.Add(Guid.NewGuid());

        Assert.Single(context.BranchIds);
    }

    [Fact]
    public void DefaultContext_HasNoPermissionsAndNoTenantWideScope()
    {
        var context = CreateContext();

        Assert.Empty(context.Permissions);
        Assert.Empty(context.PlatformPermissions);
        Assert.False(context.TenantWideScope);
    }

    [Fact]
    public void Accessor_RefusesToReplaceAnExistingContext()
    {
        using var scope = TenantContextAccessor.Begin(CreateContext());

        Assert.True(TenantContextAccessor.IsResolved);
        Assert.Throws<InvalidOperationException>(() => TenantContextAccessor.Begin(CreateContext()));
    }

    [Fact]
    public void Accessor_RequireFailsWithoutContext_AndClearsOnDispose()
    {
        Assert.Throws<InvalidOperationException>(() => TenantContextAccessor.Require());

        using (TenantContextAccessor.Begin(CreateContext()))
        {
            Assert.NotNull(TenantContextAccessor.Current);
        }

        Assert.Null(TenantContextAccessor.Current);
        Assert.False(TenantContextAccessor.IsResolved);
    }
}
