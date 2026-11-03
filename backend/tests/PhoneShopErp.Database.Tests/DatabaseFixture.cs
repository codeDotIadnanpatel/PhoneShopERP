using Npgsql;
using PhoneShopErp.Modules.IdentityAccess.Application;
using PhoneShopErp.Modules.IdentityAccess.Infrastructure;
using PhoneShopErp.Modules.Tenancy.Application;
using PhoneShopErp.Modules.Tenancy.Infrastructure;
using PhoneShopErp.Persistence;
using PhoneShopErp.Platform.Abstractions;
using PhoneShopErp.Platform.Audit;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// One migrated database for the behavioural suite. Migration-specific tests
/// provision their own database because they deliberately corrupt state.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "xUnit's IAsyncLifetime supplies the disposal hook for the fixture.")]
public sealed class DatabaseFixture : IAsyncLifetime
{
    private NpgsqlConnectionFactory? connections;

    public TestDatabase Database { get; private set; } = null!;

    public IReadOnlyList<string> AppliedScripts { get; private set; } = [];

    public IIdGenerator Ids { get; } = new UuidV7IdGenerator();

    public IClock Clock { get; } = new SystemClock();

    public IAuditStore Audit { get; } = new PostgresAuditStore();

    public PostgresTenantService Tenants { get; private set; } = null!;

    public PostgresIdentityService Identity { get; private set; } = null!;

    public IDbConnectionFactory Connections => connections!;

    public PostgresPreTenantUnitOfWorkFactory PreTenantUnitOfWorkFactory { get; private set; } = null!;

    public PostgresTenantUnitOfWorkFactory TenantUnitOfWorkFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Database = await TestEnvironment.CreateDatabaseAsync();

        var expected = Directory
            .EnumerateFiles(TestEnvironment.MigrationsRoot, "*.sql", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(TestEnvironment.MigrationsRoot, path).Replace('\\', '/'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var migration = await Database.MigrateAsync();
        Assert.Equal(expected.Length, migration.AppliedThisRun);

        AppliedScripts = expected;
        Assert.Equal(expected.Length, migration.TotalApplied);

        connections = new NpgsqlConnectionFactory(new DatabaseOptions
        {
            ConnectionString = Database.AppConnectionString,
            MaximumPoolSize = 10,
        });

        PreTenantUnitOfWorkFactory = new PostgresPreTenantUnitOfWorkFactory(connections);
        TenantUnitOfWorkFactory = new PostgresTenantUnitOfWorkFactory(connections);

        Tenants = new PostgresTenantService(
            PreTenantUnitOfWorkFactory,
            TenantUnitOfWorkFactory,
            new PostgresOnboardingRequestStore(),
            Audit,
            Ids,
            Clock);

        Identity = new PostgresIdentityService(
            PreTenantUnitOfWorkFactory,
            TenantUnitOfWorkFactory,
            Audit,
            Ids,
            Clock,
            SessionPolicy.Default);
    }

    public async Task DisposeAsync()
    {
        if (connections is not null)
        {
            await connections.DisposeAsync();
        }

        await Database.DisposeAsync();
    }

    /// <summary>Creates a tenant through the real onboarding path.</summary>
    public async Task<SeededTenant> CreateTenantAsync(string slug, string? subject = null)
    {
        subject ??= "subject-" + slug;

        var summary = await Tenants.CreateAsync(
            new TenantOwnerProfile(subject, "Owner " + slug, subject + "@example.test"),
            new CreateTenantRequest(slug, "Tenant " + slug, "en-GB", "Europe/London"),
            "onboarding-" + Guid.NewGuid().ToString("N"),
            CancellationToken.None);

        var rows = await Database.QueryAsync(
            $"SELECT m.id, m.user_id FROM identity.membership m WHERE m.tenant_id = '{summary.Id}'",
            "postgres");

        return new SeededTenant(summary.Id, (Guid)rows[0][1]!, (Guid)rows[0][0]!, subject);
    }

    public Task<NpgsqlConnection> OpenAppAsync() =>
        Task.FromResult(new NpgsqlConnection(Database.AppConnectionString));
}

[CollectionDefinition(PostgresDatabaseGroup.Name)]
public sealed class PostgresDatabaseGroup : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "postgres";
}

public sealed record SeededTenant(Guid TenantId, Guid UserId, Guid MembershipId, string Subject)
{
    public AuthenticatedPrincipal Principal(string? sessionReference = null) =>
        new(Subject, sessionReference, "Owner", Subject + "@example.test");
}
