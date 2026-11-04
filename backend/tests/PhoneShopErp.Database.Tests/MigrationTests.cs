using Npgsql;
using PhoneShopErp.Migrations;
using Xunit;

namespace PhoneShopErp.Database.Tests;

/// <summary>
/// Exercises the real migration path (not a copy): role assertion, advisory
/// lock, checksum journal, ownership assertion, clean install and re-run.
/// </summary>
public sealed class MigrationTests
{
    [Fact]
    public async Task CleanInstall_AppliesEveryScriptAndCreatesThePlatformSchema()
    {
        await using var database = await TestEnvironment.CreateDatabaseAsync();

        var outcome = await database.MigrateAsync();

        Assert.Equal(23, outcome.TotalApplied);
        Assert.Equal(23, outcome.AppliedThisRun);

        var tables = await database.ScalarAsync<long>(
            """
            SELECT count(*)
            FROM pg_tables
            WHERE schemaname IN ('platform', 'tenancy', 'identity', 'saas');
            """,
            "postgres");
        Assert.True(tables >= 29, $"Expected at least 29 managed tables, found {tables}.");

        var schemas = await database.QueryAsync(
            "SELECT nspname FROM pg_namespace WHERE nspname IN ('platform', 'tenancy', 'identity', 'saas') ORDER BY nspname;",
            "postgres");
        Assert.Equal(
            ["identity", "platform", "saas", "tenancy"],
            schemas.Select(row => (string)row[0]!).ToArray());
    }

    [Fact]
    public async Task Rerun_AppliesNothingAndKeepsTheJournalStable()
    {
        await using var database = await TestEnvironment.CreateDatabaseAsync();
        await database.MigrateAsync();

        var rerun = await database.MigrateAsync();

        Assert.Equal(0, rerun.AppliedThisRun);
        Assert.Equal(23, rerun.TotalApplied);
    }

    [Fact]
    public async Task VerifyOnly_DoesNotApplyPendingScripts()
    {
        await using var database = await TestEnvironment.CreateDatabaseAsync();
        await database.MigrateAsync();

        var verify = await database.MigrateAsync(verifyOnly: true);

        Assert.Equal(0, verify.AppliedThisRun);
        Assert.Equal(23, verify.TotalApplied);
    }

    [Fact]
    public async Task ChecksumJournal_RejectsAnEditedAppliedScript()
    {
        await using var database = await TestEnvironment.CreateDatabaseAsync();
        var scripts = CopyMigrationsToTemporaryDirectory();

        try
        {
            await database.MigrateAsync(scripts);

            var target = Path.Combine(scripts, "010-platform", "0001-schemas.sql");
            await File.AppendAllTextAsync(target, "\n-- edited after review\n");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => database.MigrateAsync(scripts));

            Assert.Contains("was modified after it ran", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(scripts, recursive: true);
        }
    }

    [Fact]
    public async Task ChecksumJournal_RejectsAnAppliedScriptThatDisappeared()
    {
        await using var database = await TestEnvironment.CreateDatabaseAsync();
        var scripts = CopyMigrationsToTemporaryDirectory();

        try
        {
            await database.MigrateAsync(scripts);

            File.Delete(Path.Combine(scripts, "010-platform", "0040-outbox.sql"));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => database.MigrateAsync(scripts));

            Assert.Contains("is missing from the migration set", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(scripts, recursive: true);
        }
    }

    [Fact]
    public async Task RoleAssertion_RejectsASuperuserMigrationCredential()
    {
        await using var database = await TestEnvironment.CreateDatabaseAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => MigrationRunner.RunAsync(
                new MigrationOptions(
                    database.AdminConnectionString,
                    "erp_owner",
                    TestEnvironment.MigrationsRoot),
                TextWriter.Null));

        Assert.Contains("Migrations must run as 'erp_owner'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RoleAssertion_RejectsASuperuserSessionThatSwitchedToTheOwnerRole()
    {
        await using var database = await TestEnvironment.CreateDatabaseAsync();

        var connectionString = new NpgsqlConnectionStringBuilder(database.AdminConnectionString)
        {
            Pooling = false,
            Options = "-c role=erp_owner",
        }.ConnectionString;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => MigrationRunner.RunAsync(
                new MigrationOptions(connectionString, "erp_owner", TestEnvironment.MigrationsRoot),
                TextWriter.Null));

        Assert.Contains("must not run as a superuser", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationConnection_AlwaysDisablesPooling()
    {
        var normalised = MigrationConnection.Normalize(
            "Host=localhost;Database=x;Username=erp_migrator;Pooling=true;MaxPoolSize=100");

        var builder = new NpgsqlConnectionStringBuilder(normalised);
        Assert.False(builder.Pooling);
    }

    [Fact]
    public async Task AdvisoryLock_SerialisesASecondMigrationRun()
    {
        await using var database = await TestEnvironment.CreateDatabaseAsync();

        // Hold the same key the runner uses, from a different connection in the
        // same database, then prove a second runner blocks instead of racing.
        await using var holder = new NpgsqlConnection(database.ConnectionStringFor("postgres"));
        await holder.OpenAsync();
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(@key);", holder))
        {
            acquire.Parameters.AddWithValue("key", AdvisoryLock.LockKey);
            await acquire.ExecuteNonQueryAsync();
        }

        var run = Task.Run(() => database.MigrateAsync());

        var waiting = await WaitForWaitingAdvisoryLockAsync(database);
        Assert.True(waiting, "The second migration run did not wait for the advisory lock.");
        Assert.False(run.IsCompleted);

        await using (var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key);", holder))
        {
            release.Parameters.AddWithValue("key", AdvisoryLock.LockKey);
            await release.ExecuteNonQueryAsync();
        }

        var outcome = await run.WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(23, outcome.AppliedThisRun);
    }

    [Fact]
    public async Task OwnershipAssertion_RequiresEveryManagedObjectToBeOwnedByTheMigrationOwner()
    {
        await using var database = await TestEnvironment.CreateDatabaseAsync();
        await database.MigrateAsync();

        var rows = await database.QueryAsync(
            """
            SELECT kind, name, owner
            FROM (
                SELECT 'schema' AS kind, n.nspname AS name, pg_get_userbyid(n.nspowner) AS owner
                FROM pg_namespace n
                WHERE n.nspname IN ('platform', 'tenancy', 'identity', 'saas')
                UNION ALL
                SELECT 'relation', n.nspname || '.' || c.relname, pg_get_userbyid(c.relowner)
                FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname IN ('platform', 'tenancy', 'identity', 'saas')
                UNION ALL
                SELECT 'function', n.nspname || '.' || p.proname, pg_get_userbyid(p.proowner)
                FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
                WHERE n.nspname IN ('platform', 'tenancy', 'identity', 'saas')
                UNION ALL
                SELECT 'type', n.nspname || '.' || t.typname, pg_get_userbyid(t.typowner)
                FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
                WHERE n.nspname IN ('platform', 'tenancy', 'identity', 'saas')
            ) objects
            WHERE NOT (kind = 'function' AND name = 'platform.tenant_ids' AND owner = 'erp_platform_admin')
              AND owner <> 'erp_owner'
            ORDER BY kind, name;
            """,
            "postgres");

        Assert.Empty(rows);
    }

    private static async Task<bool> WaitForWaitingAdvisoryLockAsync(TestDatabase database)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var waiting = await database.ScalarAsync<long>(
                "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted;",
                "postgres");

            if (waiting > 0)
            {
                return true;
            }

            await Task.Delay(50);
        }

        return false;
    }

    private static string CopyMigrationsToTemporaryDirectory()
    {
        var destination = Path.Combine(
            Path.GetTempPath(),
            "erp-migrations-" + Guid.NewGuid().ToString("N"));

        foreach (var source in Directory.EnumerateFiles(
            TestEnvironment.MigrationsRoot,
            "*.sql",
            SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(TestEnvironment.MigrationsRoot, source);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }

        return destination;
    }
}
