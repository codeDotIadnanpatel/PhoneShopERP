using System.Data;
using DbUp.Engine;
using Npgsql;

namespace PhoneShopErp.Migrations;

/// <summary>
/// Own journal with a checksum column. DbUp's stock journal records only names,
/// which cannot detect an edited already-applied script. This journal:
///   * creates the journal table before the first script,
///   * records SHA-256 of the normalised script contents,
///   * refuses to run when an applied script changed or disappeared,
///   * keeps the journal row in the same transaction as the script when DbUp is
///     configured with <c>WithTransactionPerScript</c>.
/// </summary>
public sealed class ChecksumJournal : IJournal
{
    private const string SchemaName = "platform";
    private const string TableName = "schema_migration";
    private const string QualifiedTableName = SchemaName + "." + TableName;

    private readonly string connectionString;

    public ChecksumJournal(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
    }

    public void EnsureTableExistsAndIsLatestVersion(Func<IDbCommand> dbCommandFactory)
    {
        ArgumentNullException.ThrowIfNull(dbCommandFactory);

        using var command = dbCommandFactory();
        command.CommandText = $"""
            CREATE SCHEMA IF NOT EXISTS {SchemaName} AUTHORIZATION erp_owner;

            CREATE TABLE IF NOT EXISTS {QualifiedTableName} (
                script_name text NOT NULL,
                checksum text NOT NULL,
                applied_at timestamptz NOT NULL DEFAULT now(),
                applied_by text NOT NULL DEFAULT current_user,
                duration_ms integer NULL,
                CONSTRAINT pk_schema_migration PRIMARY KEY (script_name),
                CONSTRAINT ck_schema_migration_checksum CHECK (checksum ~ '^[0-9a-f]+$' AND length(checksum) = 64)
            );

            REVOKE ALL ON TABLE {QualifiedTableName} FROM PUBLIC;
            """;
        _ = command.ExecuteNonQuery();
    }

    public string[] GetExecutedScripts()
    {
        var names = new List<string>();

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(
            $"SELECT script_name FROM {QualifiedTableName} ORDER BY script_name;",
            connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return [.. names];
    }

    public void StoreExecutedScript(SqlScript script, Func<IDbCommand> dbCommandFactory)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(dbCommandFactory);

        using var command = dbCommandFactory();
        command.CommandText = $"""
            INSERT INTO {QualifiedTableName} (script_name, checksum)
            VALUES (@script_name, @checksum);
            """;

        var nameParameter = command.CreateParameter();
        nameParameter.ParameterName = "script_name";
        nameParameter.Value = script.Name;
        _ = command.Parameters.Add(nameParameter);

        var checksumParameter = command.CreateParameter();
        checksumParameter.ParameterName = "checksum";
        checksumParameter.Value = MigrationScriptProvider.Checksum(script);
        _ = command.Parameters.Add(checksumParameter);

        _ = command.ExecuteNonQuery();
    }

    /// <summary>
    /// Fails if an applied script has been edited or removed, or if the journal
    /// contains a script that is not part of the reviewed set. Must run before
    /// DbUp starts executing anything.
    /// </summary>
    public void VerifyChecksums(IReadOnlyList<SqlScript> scripts)
    {
        ArgumentNullException.ThrowIfNull(scripts);

        var expected = scripts.ToDictionary(
            script => script.Name,
            MigrationScriptProvider.Checksum,
            StringComparer.Ordinal);

        var applied = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var connection = new NpgsqlConnection(connectionString))
        {
            connection.Open();
            using var command = new NpgsqlCommand(
                $"SELECT script_name, checksum FROM {QualifiedTableName};",
                connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                applied[reader.GetString(0)] = reader.GetString(1);
            }
        }

        var problems = new List<string>();

        foreach (var (name, checksum) in applied)
        {
            if (!expected.TryGetValue(name, out var currentChecksum))
            {
                problems.Add($"applied script '{name}' is missing from the migration set");
            }
            else if (!string.Equals(checksum, currentChecksum, StringComparison.Ordinal))
            {
                problems.Add($"applied script '{name}' was modified after it ran");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "Migration checksum verification failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, problems.Select(problem => "  - " + problem)));
        }
    }

    /// <summary>
    /// Returns the applied script names, for CI evidence. Read-only.
    /// </summary>
    public IReadOnlyList<string> AppliedScripts() => GetExecutedScripts();
}
