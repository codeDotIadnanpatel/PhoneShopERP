using PhoneShopErp.Modules.IdentityAccess.Application;
using PhoneShopErp.Platform.Errors;
using Xunit;

namespace PhoneShopErp.Platform.Tests;

public sealed class SessionPolicyTests
{
    [Fact]
    public void Default_MatchesTheApprovedBackOfficeBounds()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), SessionPolicy.Default.IdleTimeout);
        Assert.Equal(TimeSpan.FromHours(12), SessionPolicy.Default.AbsoluteTimeout);
    }

    [Theory]
    [InlineData(4, 60)]
    [InlineData(1500, 2000)]
    public void Constructor_RejectsAnIdleTimeoutOutsideTheBoundedRange(int idleMinutes, int absoluteMinutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionPolicy(
            TimeSpan.FromMinutes(idleMinutes),
            TimeSpan.FromMinutes(absoluteMinutes)));
    }

    [Fact]
    public void Constructor_RejectsAnAbsoluteTimeoutShorterThanTheIdleTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionPolicy(
            TimeSpan.FromHours(1),
            TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void Constructor_RejectsAnAbsoluteTimeoutBeyondThirtyDays()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionPolicy(
            TimeSpan.FromMinutes(30),
            TimeSpan.FromDays(31)));
    }

    [Fact]
    public void Constructor_AcceptsTheBoundaryValues()
    {
        var shortest = new SessionPolicy(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        var longest = new SessionPolicy(TimeSpan.FromHours(24), TimeSpan.FromDays(30));

        Assert.Equal(TimeSpan.FromMinutes(5), shortest.IdleTimeout);
        Assert.Equal(TimeSpan.FromDays(30), longest.AbsoluteTimeout);
    }

    [Fact]
    public void AuthorizationRules_TenantAndPlatformSetsNeverMix()
    {
        var session = ResolvedSessionFor(["tenant.view"], ["security_event.view"]);

        Assert.True(AuthorizationRules.HasTenantPermission(session, "tenant.view"));
        Assert.False(AuthorizationRules.HasPlatformPermission(session, "tenant.view"));
        Assert.True(AuthorizationRules.HasPlatformPermission(session, "security_event.view"));
        Assert.False(AuthorizationRules.HasTenantPermission(session, "security_event.view"));
    }

    [Fact]
    public void AuthorizationRules_DemandPlatformFailsClosed()
    {
        var session = ResolvedSessionFor(["tenant.view"], []);

        var exception = Assert.Throws<PlatformException>(
            () => AuthorizationRules.DemandPlatform(session, "tenant.create"));

        Assert.Equal(ErrorCodes.Forbidden, exception.Code);
    }

    [Theory]
    [InlineData("tenant", null)]
    [InlineData("branch", "8f1d5c4b-6b7c-4d8e-9a10-2f3c4d5e6f70")]
    [InlineData("legal_entity", "8f1d5c4b-6b7c-4d8e-9a10-2f3c4d5e6f70")]
    [InlineData("stock_location", "8f1d5c4b-6b7c-4d8e-9a10-2f3c4d5e6f70")]
    public void ValidateScope_AcceptsCoherentAssignments(string scopeType, string? scopeId)
    {
        AuthorizationRules.ValidateScope(
            scopeType,
            scopeId is null ? null : Guid.Parse(scopeId));
    }

    [Theory]
    [InlineData("warehouse", null)]
    [InlineData("tenant", "8f1d5c4b-6b7c-4d8e-9a10-2f3c4d5e6f70")]
    [InlineData("branch", null)]
    [InlineData("branch", "00000000-0000-0000-0000-000000000000")]
    public void ValidateScope_RejectsIncoherentAssignments(string scopeType, string? scopeId)
    {
        var exception = Assert.Throws<PlatformException>(() => AuthorizationRules.ValidateScope(
            scopeType,
            scopeId is null ? null : Guid.Parse(scopeId)));

        Assert.Equal(ErrorCodes.ValidationFailed, exception.Code);
    }

    [Fact]
    public void SessionRequestMetadata_HashesBindingMaterialInsteadOfStoringIt()
    {
        var metadata = new SessionRequestMetadata("desk-1", "windows", "agent/1", "10.0.0.7");

        var fingerprint = metadata.BindingFingerprint();

        Assert.Equal(64, fingerprint.Length);
        Assert.DoesNotContain("10.0.0.7", fingerprint, StringComparison.Ordinal);
        Assert.Equal(fingerprint, metadata.BindingFingerprint());
        Assert.NotEqual(
            fingerprint,
            new SessionRequestMetadata("desk-2", "windows", "agent/1", "10.0.0.7")
                .BindingFingerprint());
    }

    private static ResolvedSession ResolvedSessionFor(
        IEnumerable<string> permissions,
        IEnumerable<string> platformPermissions) =>
        ResolvedSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            null,
            true,
            permissions,
            platformPermissions,
            [],
            [],
            []);
}
