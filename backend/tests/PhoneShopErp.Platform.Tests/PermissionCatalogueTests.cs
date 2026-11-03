using PhoneShopErp.Platform.Security;
using Xunit;

namespace PhoneShopErp.Platform.Tests;

public sealed class PermissionCatalogueTests
{
    [Fact]
    public void Catalogue_ContainsNoDuplicateCodes()
    {
        Assert.Equal(
            PermissionCatalogue.All.Count,
            PermissionCatalogue.All.Select(definition => definition.Code).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Catalogue_CodesMatchTheDatabaseCheckConstraint()
    {
        // Mirrors ck_permission_code in 030-identity/0040-authorization.sql.
        var pattern = new System.Text.RegularExpressions.Regex(
            @"^[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*$");

        Assert.All(PermissionCatalogue.All, definition =>
            Assert.Matches(pattern, definition.Code));
    }

    [Fact]
    public void Catalogue_UsesOnlyTheTwoApprovedScopes()
    {
        Assert.All(PermissionCatalogue.All, definition =>
            Assert.True(
                definition.Scope is PermissionCatalogue.TenantScope or PermissionCatalogue.PlatformScope,
                $"{definition.Code} has scope '{definition.Scope}'."));
    }

    [Fact]
    public void Catalogue_PlatformPermissionsAreExactlyTheThreePlatformAdministrationCodes()
    {
        var platform = PermissionCatalogue.All
            .Where(definition => definition.Scope == PermissionCatalogue.PlatformScope)
            .Select(definition => definition.Code)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["security_event.view", "tenant.create", "tenant.suspend"],
            platform);
    }

    [Fact]
    public void Catalogue_EveryHighRiskPermissionIsProtected()
    {
        Assert.All(
            PermissionCatalogue.All.Where(definition => definition.RiskClass == PermissionRiskClass.High),
            definition => Assert.True(definition.IsProtected, $"{definition.Code} is high risk but unprotected."));
    }

    [Fact]
    public void Catalogue_ProtectedPermissionsAreHighRisk()
    {
        Assert.All(
            PermissionCatalogue.All.Where(definition => definition.IsProtected),
            definition => Assert.Equal(PermissionRiskClass.High, definition.RiskClass));
    }

    [Fact]
    public void Catalogue_ContainsNoBusinessModulePermissionsDuringPhase01()
    {
        string[] forbiddenPrefixes =
        [
            "catalogue.", "inventory.", "purchase.", "pos.", "tradein.", "trade_in.",
            "repair.", "accounting.", "ledger.",
        ];

        Assert.All(PermissionCatalogue.All, definition =>
            Assert.DoesNotContain(
                forbiddenPrefixes,
                prefix => definition.Code.StartsWith(prefix, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("tenant.create")]
    [InlineData("tenant.suspend")]
    [InlineData("security_event.view")]
    public void IsPlatformScope_IsTrueOnlyForPlatformAdministration(string code)
    {
        Assert.True(PermissionCatalogue.IsPlatformScope(code));
    }

    [Theory]
    [InlineData("tenant.view")]
    [InlineData("session.revoke")]
    [InlineData("not.a.permission")]
    public void IsPlatformScope_IsFalseForTenantAndUnknownCodes(string code)
    {
        Assert.False(PermissionCatalogue.IsPlatformScope(code));
    }

    [Fact]
    public void Lookups_AreOrdinalAndFailClosed()
    {
        Assert.True(PermissionCatalogue.IsKnown("tenant.view"));
        Assert.False(PermissionCatalogue.IsKnown("TENANT.VIEW"));
        Assert.False(PermissionCatalogue.IsKnown("tenant.view.all"));
        Assert.False(PermissionCatalogue.TryGet("TENANT.VIEW", out _));
        Assert.False(PermissionCatalogue.RequiresHighRiskMfa("not.a.permission"));
    }

    [Fact]
    public void RequiresHighRiskMfa_FollowsRiskClass()
    {
        Assert.True(PermissionCatalogue.RequiresHighRiskMfa("tenant.suspend"));
        Assert.False(PermissionCatalogue.RequiresHighRiskMfa("tenant.view"));
    }
}
