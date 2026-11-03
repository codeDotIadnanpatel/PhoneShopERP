using System.Globalization;
using PhoneShopErp.Migrations;

// -----------------------------------------------------------------------------
// Purpose-built migration runner (ADR-0014, P01-WP01).
//
// It is a console application, never referenced by the API, and never invoked at
// application startup. It requires an explicit migration credential, asserts the
// role model, takes a blocking advisory lock, verifies every applied checksum and
// only then applies pending scripts in path order.
//
// The same work is exposed as `MigrationRunner` so integration tests execute the
// production code path instead of a test-only copy.
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

try
{
    var outcome = await MigrationRunner.RunAsync(
        new MigrationOptions(connectionString, expectedOwner, scriptsRoot, verifyOnly),
        Console.Out).ConfigureAwait(false);

    Console.WriteLine(
        $"Migration complete at {DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)}. " +
        $"{outcome.AppliedThisRun} script(s) applied, {outcome.TotalApplied} total.");
    return 0;
}
catch (MigrationFailedException exception)
{
    Console.Error.WriteLine($"Migration failed: {exception.Message}");
    return 1;
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
