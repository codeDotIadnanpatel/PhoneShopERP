using Npgsql;
using PhoneShopErp.Migrations;

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// Location and provisioning of throwaway PostgreSQL databases. The admin
/// connection string comes from <c>ERPNET_TEST_POSTGRES_ADMIN</c>; the default
/// matches the local verification cluster documented in
/// <c>docs/13-audit-remediation/PHASE_01_VERIFICATION_REPORT.md</c>.
/// </summary>
public static class TestEnvironment
{
    public static string AdminConnectionString { get; } =
        Environment.GetEnvironmentVariable("ERPNET_TEST_POSTGRES_ADMIN")
        ?? "Host=localhost;Port=55432;Database=postgres;Username=postgres;Pooling=false";

    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string BootstrapScriptPath =>
        Path.Combine(RepositoryRoot, "database", "bootstrap", "0001-roles.sql");

    public static string MigrationsRoot => Path.Combine(RepositoryRoot, "database", "migrations");

    /// <summary>
    /// Creates an empty database, applies the real bootstrap script and returns a
    /// handle. Nothing is migrated yet: the caller decides whether to run the
    /// production migration path.
    /// </summary>
    public static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var name = "erp_test_" + Guid.NewGuid().ToString("N")[..12];

        await using (var admin = new NpgsqlConnection(AdminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name};", admin);
            await create.ExecuteNonQueryAsync();
        }

        var database = new TestDatabase(name);
        try
        {
            await database.ApplyBootstrapAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "database", "bootstrap", "0001-roles.sql")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate 'database/bootstrap/0001-roles.sql' above the test output directory.");
    }
}

public sealed class TestDatabase : IAsyncDisposable
{
    private bool dropped;

    public TestDatabase(string name)
    {
        Name = name;
        AdminConnectionString = new NpgsqlConnectionStringBuilder(TestEnvironment.AdminConnectionString)
        {
            Database = name,
        }.ConnectionString;
    }

    public string Name { get; }

    public string AdminConnectionString { get; }

    public MigrationOutcome? LastMigration { get; set; }

    public string ConnectionStringFor(string role) =>
        new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Username = role,
            Password = null,
            Pooling = false,
        }.ConnectionString;

    /// <summary>Connection string with pooling left enabled, for leakage tests.</summary>
    public string PooledConnectionStringFor(string role, int maxPoolSize = 1) =>
        new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Username = role,
            Password = null,
            Pooling = true,
            MaxPoolSize = maxPoolSize,
        }.ConnectionString;

    public string AppConnectionString => ConnectionStringFor("erp_app");

    public async Task ApplyBootstrapAsync()
    {
        var sql = await File.ReadAllTextAsync(TestEnvironment.BootstrapScriptPath);
        await ExecuteAsync(sql, "postgres");
    }

    public Task<MigrationOutcome> MigrateAsync(
        string? scriptsRoot = null,
        bool verifyOnly = false,
        string expectedOwner = "erp_owner")
    {
        LastMigration = null;
        return RunMigrationAsync(scriptsRoot ?? TestEnvironment.MigrationsRoot, verifyOnly, expectedOwner);
    }

    private async Task<MigrationOutcome> RunMigrationAsync(
        string scriptsRoot,
        bool verifyOnly,
        string expectedOwner)
    {
        var outcome = await MigrationRunner.RunAsync(
            new MigrationOptions(
                ConnectionStringFor("erp_migrator"),
                expectedOwner,
                scriptsRoot,
                verifyOnly),
            TextWriter.Null);

        LastMigration = outcome;
        return outcome;
    }

    public async Task ExecuteAsync(string sql, string role = "erp_app")
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor(role));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<T?> ScalarAsync<T>(string sql, string role = "erp_app")
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor(role));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    public async Task<List<object?[]>> QueryAsync(string sql, string role = "erp_app")
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor(role));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<object?[]>();
        while (await reader.ReadAsync())
        {
            var values = new object?[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                values[index] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            rows.Add(values);
        }

        return rows;
    }

    public async ValueTask DisposeAsync()
    {
        if (dropped)
        {
            return;
        }

        dropped = true;
        NpgsqlConnection.ClearAllPools();

        try
        {
            await using var admin = new NpgsqlConnection(TestEnvironment.AdminConnectionString);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {Name} WITH (FORCE);", admin);
            await drop.ExecuteNonQueryAsync();
        }
        catch (NpgsqlException)
        {
            // A best-effort cleanup must not hide the test result.
        }
    }
}
