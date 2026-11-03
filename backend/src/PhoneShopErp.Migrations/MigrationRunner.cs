using DbUp;
using Npgsql;

namespace PhoneShopErp.Migrations;

/// <summary>
/// Reusable production migration path. Tests call this directly so the role
/// assertion, advisory lock, checksum verification and DbUp execution they
/// exercise are the real ones, not a test-only copy.
/// </summary>
public static class MigrationRunner
{
    public static async Task<MigrationOutcome> RunAsync(
        MigrationOptions options,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);

        var scripts = MigrationScriptProvider.Load(options.ScriptsRoot);

        // Session state matters here. erp_migrator logs in with
        // `ALTER ROLE ... SET role = erp_owner`, so every *fresh* session starts as
        // erp_owner and migration objects are owned by that role. A pooled Npgsql
        // connection is reset with `DISCARD ALL` before reuse, and `DISCARD ALL`
        // reverts current_user to the login role, silently running scripts as
        // erp_migrator. Pooling is therefore disabled for the whole migration run.
        var connectionString = MigrationConnection.Normalize(options.ConnectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await RoleAssertion.AssertAsync(connection, options.ExpectedOwner, output, cancellationToken)
            .ConfigureAwait(false);

        var journal = new ChecksumJournal(connectionString);

        await AdvisoryLock.AcquireAsync(connection, output, cancellationToken).ConfigureAwait(false);

        try
        {
            journal.EnsureTableExistsAndIsLatestVersion(
                () =>
                {
                    var command = connection.CreateCommand();
                    return command;
                });

            journal.VerifyChecksums(scripts);

            if (options.VerifyOnly)
            {
                var applied = journal.AppliedScripts();
                output.WriteLine($"Checksums verified. {applied.Count} script(s) applied.");
                return new MigrationOutcome(0, applied.Count);
            }

            var engine = DeployChanges.To
                .PostgresqlDatabase(connectionString)
                .WithScripts(scripts)
                // PostgreSQL dollar-quoted bodies ($fn$ ... $fn$) look exactly like
                // DbUp variable placeholders. The reviewed SQL is literal; variable
                // substitution must stay off or function bodies fail to parse.
                .WithVariablesDisabled()
                .JournalTo(journal)
                .WithTransactionPerScript()
                .LogToConsole()
                .Build();

            var result = engine.PerformUpgrade();
            if (!result.Successful)
            {
                throw new MigrationFailedException(
                    result.Error?.Message ?? "the migration engine reported a failure",
                    result.Error);
            }

            // Executing the scripts is not proof that they ran under the expected
            // role. Ownership is a security boundary (forced RLS applies to the
            // owner), so it is verified rather than assumed.
            await OwnershipAssertion.AssertAsync(connection, options.ExpectedOwner, output, cancellationToken)
                .ConfigureAwait(false);

            return new MigrationOutcome(result.Scripts.Count(), journal.AppliedScripts().Count);
        }
        finally
        {
            await AdvisoryLock.ReleaseAsync(connection).ConfigureAwait(false);
        }
    }
}

public sealed record MigrationOptions(
    string ConnectionString,
    string ExpectedOwner,
    string ScriptsRoot,
    bool VerifyOnly = false);

public sealed record MigrationOutcome(int AppliedThisRun, int TotalApplied);

internal static class MigrationConnection
{
    /// <summary>
    /// Returns the connection string the migration run must use. Pooling is
    /// disabled because a pooled-connection reset runs <c>DISCARD ALL</c>, which
    /// drops the <c>SET ROLE</c> applied by <c>ALTER ROLE erp_migrator SET role</c>
    /// and would let scripts create objects as erp_migrator. A migration run opens
    /// a handful of connections; pooling buys nothing and silently weakens the
    /// ownership model.
    /// </summary>
    public static string Normalize(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
        };

        return builder.ConnectionString;
    }
}

internal static class OwnershipAssertion
{
    private static readonly string[] ManagedSchemas = ["platform", "tenancy", "identity", "saas"];

    /// <summary>
    /// Reviewed exceptions to "everything is owned by the migration owner". The
    /// migration-only enumeration function must be owned by the BYPASSRLS role,
    /// because forced RLS applies to erp_owner. Any other deviation fails.
    /// </summary>
    private static readonly Dictionary<(string Kind, string Name), string> ReviewedExceptions = new()
    {
        [("function", "platform.tenant_ids")] = "erp_platform_admin",
    };

    /// <summary>
    /// Fails the migration if any schema, relation, sequence, function or type in
    /// the ERP-managed schemas is not owned by the expected owner role.
    /// </summary>
    public static async Task AssertAsync(
        NpgsqlConnection connection,
        string expectedOwner,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH managed AS (
                SELECT n.oid, n.nspname, n.nspowner
                FROM pg_namespace n
                WHERE n.nspname = ANY(@schemas)
            ),
            owned AS (
                SELECT 'schema' AS kind, n.nspname AS name, pg_get_userbyid(n.nspowner) AS owner
                FROM managed n
                UNION ALL
                SELECT 'relation', n.nspname || '.' || c.relname, pg_get_userbyid(c.relowner)
                FROM pg_class c
                JOIN managed n ON n.oid = c.relnamespace
                UNION ALL
                SELECT 'function', n.nspname || '.' || p.proname, pg_get_userbyid(p.proowner)
                FROM pg_proc p
                JOIN managed n ON n.oid = p.pronamespace
                UNION ALL
                SELECT 'type', n.nspname || '.' || t.typname, pg_get_userbyid(t.typowner)
                FROM pg_type t
                JOIN managed n ON n.oid = t.typnamespace
            )
            SELECT kind, name, owner
            FROM owned
            WHERE owner <> @owner
            ORDER BY kind, name;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schemas", ManagedSchemas);
        command.Parameters.AddWithValue("owner", expectedOwner);

        var offenders = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var kind = reader.GetString(0);
                var name = reader.GetString(1);
                var owner = reader.GetString(2);

                if (ReviewedExceptions.TryGetValue((kind, name), out var allowedOwner) &&
                    string.Equals(owner, allowedOwner, StringComparison.Ordinal))
                {
                    continue;
                }

                offenders.Add($"{kind} {name} (owner {owner})");
            }
        }

        if (offenders.Count > 0)
        {
            throw new InvalidOperationException(
                $"Migration ownership assertion failed: objects must be owned by '{expectedOwner}'." +
                Environment.NewLine +
                string.Join(Environment.NewLine, offenders.Select(offender => "  - " + offender)));
        }

        output.WriteLine($"Ownership assertion passed: all managed objects are owned by '{expectedOwner}'.");
    }
}

public sealed class MigrationFailedException : Exception
{
    public MigrationFailedException()
    {
    }

    public MigrationFailedException(string message)
        : base(message)
    {
    }

    public MigrationFailedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

internal static class AdvisoryLock
{
    // Arbitrary, stable key. Only this runner uses it.
    // Shared with PhoneShopErp.Database.Tests so the serialisation test can hold
    // the same key deliberately.
    internal const long LockKey = 0x5048_4552_5001;

    public static async Task AcquireAsync(
        NpgsqlConnection connection,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(@key);", connection);
        command.Parameters.AddWithValue("key", LockKey);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        output.WriteLine("Advisory migration lock acquired.");
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
    public static async Task AssertAsync(
        NpgsqlConnection connection,
        string expectedOwner,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT session_user,
                   current_user,
                   (SELECT rolsuper FROM pg_roles WHERE rolname = current_user),
                   (SELECT rolbypassrls FROM pg_roles WHERE rolname = current_user),
                   (SELECT rolsuper FROM pg_roles WHERE rolname = session_user);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
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

        output.WriteLine(
            $"Role assertion passed: session_user='{sessionUser}', current_user='{currentUser}'.");
    }
}
