using System.Globalization;
using DbUp;
using DbUp.Engine;
using Npgsql;
using PhoneShopErp.Migrations;

// -----------------------------------------------------------------------------
// Purpose-built migration runner (ADR-0014, P01-WP01).
//
// It is a console application, never referenced by the API, and never invoked at
// application startup. It requires an explicit migration credential, asserts the
// role model, takes a blocking advisory lock, verifies every applied checksum and
// only then applies pending scripts in path order.
// -----------------------------------------------------------------------------

var printScripts = args.Contains("--print-scripts", StringComparer.Ordinal);
var verifyOnly = args.Contains("--verify-only", StringComparer.Ordinal);

var connectionString = Environment.GetEnvironmentVariable("ERP_MIGRATION_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "ERP_MIGRATION_CONNECTION is required and must use the erp_migrator credential.");
    return 2;
}

var expectedOwner = Environment.GetEnvironmentVariable("ERP_MIGRATION_OWNER") ?? "erp_owner";
var scriptsRoot = ResolveScriptsRoot(args);
var scripts = MigrationScriptProvider.Load(scriptsRoot);

Console.WriteLine(
    $"Migration set: {scripts.Count} script(s) from '{scriptsRoot}' (owner '{expectedOwner}').");

if (printScripts)
{
    foreach (var script in scripts)
    {
        Console.WriteLine($"  {MigrationScriptProvider.Checksum(script)}  {script.Name}");
    }
}

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync().ConfigureAwait(false);

await RoleAssertion.AssertAsync(connection, expectedOwner).ConfigureAwait(false);

var journal = new ChecksumJournal(connectionString);

// A second runner waits here, then sees the completed journal and applies
// nothing. The lock lives on this dedicated connection for the whole run and is
// released by PostgreSQL if the process dies.
await AdvisoryLock.AcquireAsync(connection).ConfigureAwait(false);

try
{
    journal.EnsureTableExistsAndIsLatestVersion(
        () =>
        {
            var command = connection.CreateCommand();
            return command;
        });

    journal.VerifyChecksums(scripts);

    if (verifyOnly)
    {
        var applied = journal.AppliedScripts();
        Console.WriteLine($"Checksums verified. {applied.Count} script(s) applied.");
        return 0;
    }

    var engine = DeployChanges.To
        .PostgresqlDatabase(connectionString)
        .WithScripts(scripts)
        .JournalTo(journal)
        .WithTransactionPerScript()
        .LogToConsole()
        .Build();

    var result = engine.PerformUpgrade();
    if (!result.Successful)
    {
        Console.Error.WriteLine($"Migration failed: {result.Error?.Message}");
        if (result.Error is not null)
        {
            Console.Error.WriteLine(result.Error);
        }

        return 1;
    }

    Console.WriteLine(
        $"Migration complete at {DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)}. " +
        $"{result.Scripts.Count()} script(s) applied, {journal.AppliedScripts().Count} total.");
    return 0;
}
finally
{
    await AdvisoryLock.ReleaseAsync(connection).ConfigureAwait(false);
}

static string ResolveScriptsRoot(string[] arguments)
{
    var index = Array.IndexOf(arguments, "--scripts");
    if (index >= 0 && index + 1 < arguments.Length)
    {
        return Path.GetFullPath(arguments[index + 1]);
    }

    var configured = Environment.GetEnvironmentVariable("ERP_MIGRATION_SCRIPTS");
    if (!string.IsNullOrWhiteSpace(configured))
    {
        return Path.GetFullPath(configured);
    }

    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        var candidate = Path.Combine(directory.FullName, "database", "migrations");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException(
        "Could not locate 'database/migrations'. Pass --scripts <path> or set ERP_MIGRATION_SCRIPTS.");
}

internal static class AdvisoryLock
{
    // Arbitrary, stable key. Only this runner uses it.
    private const long LockKey = 0x5048_4552_5001;

    public static async Task AcquireAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(@key);", connection);
        command.Parameters.AddWithValue("key", LockKey);
        _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        Console.WriteLine("Advisory migration lock acquired.");
    }

    public static async Task ReleaseAsync(NpgsqlConnection connection)
    {
        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key);", connection);
            command.Parameters.AddWithValue("key", LockKey);
            _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            // The connection is already gone; PostgreSQL released the lock.
        }
    }
}

internal static class RoleAssertion
{
    public static async Task AssertAsync(NpgsqlConnection connection, string expectedOwner)
    {
        const string sql = """
            SELECT session_user,
                   current_user,
                   (SELECT rolsuper FROM pg_roles WHERE rolname = current_user),
                   (SELECT rolbypassrls FROM pg_roles WHERE rolname = current_user),
                   (SELECT rolsuper FROM pg_roles WHERE rolname = session_user);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        if (!await reader.ReadAsync().ConfigureAwait(false))
        {
            throw new InvalidOperationException("Could not read the current database role.");
        }

        var sessionUser = reader.GetString(0);
        var currentUser = reader.GetString(1);
        var currentSuper = reader.GetBoolean(2);
        var currentBypassRls = reader.GetBoolean(3);
        var sessionSuper = reader.GetBoolean(4);

        if (!string.Equals(currentUser, expectedOwner, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Migrations must run as '{expectedOwner}' (objects are owned by that role), " +
                $"but current_user is '{currentUser}'.");
        }

        if (currentSuper || sessionSuper)
        {
            throw new InvalidOperationException(
                "Migrations must not run as a superuser. Grant erp_owner to erp_migrator and use erp_migrator.");
        }

        if (currentBypassRls)
        {
            throw new InvalidOperationException(
                "The migration owner must not have BYPASSRLS. Forced RLS must apply to the owner.");
        }

        Console.WriteLine(
            $"Role assertion passed: session_user='{sessionUser}', current_user='{currentUser}'.");
    }
}
