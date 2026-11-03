using PhoneShopErp.Platform.Security;
using Xunit;

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// The in-code permission catalogue is used to validate seeds and to classify
/// risk. It must match the database, which is authoritative at runtime.
/// </summary>
[Collection(PostgresDatabaseGroup.Name)]
public sealed class PermissionCatalogueParityTests
{
    private readonly DatabaseFixture fixture;

    public PermissionCatalogueParityTests(DatabaseFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Catalogue_MatchesTheSeededPermissionTable()
    {
        var rows = await fixture.Database.QueryAsync(
            "SELECT code, display_name, risk_class, scope, is_protected FROM identity.permission ORDER BY code;",
            "postgres");

        Assert.Equal(PermissionCatalogue.All.Count, rows.Count);

        foreach (var row in rows)
        {
            var code = (string)row[0]!;
            Assert.True(PermissionCatalogue.TryGet(code, out var definition), $"{code} is in the database but not the catalogue.");

            Assert.Equal((string)row[1]!, definition.DisplayName);
            Assert.Equal((string)row[2]!, definition.RiskClass.ToString().ToLowerInvariant());
            Assert.Equal((string)row[3]!, definition.Scope);
            Assert.Equal((bool)row[4]!, definition.IsProtected);
        }
    }

    [Fact]
    public async Task Catalogue_HasNoEntryMissingFromTheDatabase()
    {
        var codes = await fixture.Database.QueryAsync(
            "SELECT code FROM identity.permission;",
            "postgres");
        var seeded = codes.Select(row => (string)row[0]!).ToHashSet(StringComparer.Ordinal);

        Assert.All(
            PermissionCatalogue.All,
            definition => Assert.Contains(definition.Code, seeded));
    }

    [Fact]
    public async Task OnlyPlatformScopedPermissions_CanBeGrantedThroughAPlatformGrant()
    {
        var rows = await fixture.Database.QueryAsync(
            "SELECT code, scope FROM identity.permission ORDER BY code;",
            "postgres");

        foreach (var row in rows)
        {
            var code = (string)row[0]!;
            var expected = PermissionCatalogue.IsPlatformScope(code);
            Assert.Equal(expected, (string)row[1]! == PermissionCatalogue.PlatformScope);
        }
    }
}
