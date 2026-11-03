using Npgsql;

namespace PhoneShopErp.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;

    public int MaximumPoolSize { get; set; } = 20;

    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Rejects connection strings that would put the runtime on an owning,
    /// migration, platform-administration or superuser credential. A runtime
    /// connection must name <c>erp_app</c> explicitly; a username supplied only
    /// through the <c>PGUSER</c> environment variable cannot be validated and is
    /// therefore rejected rather than trusted.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                "Database:ConnectionString is required. The runtime must connect as erp_app.");
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "Database:ConnectionString is not a valid PostgreSQL connection string.",
                exception);
        }

        if (string.IsNullOrWhiteSpace(builder.Username))
        {
            throw new InvalidOperationException(
                "Database:ConnectionString must name Username=erp_app explicitly. A username " +
                "supplied only through PGUSER cannot be validated, so it is rejected.");
        }

        var username = builder.Username.Trim();
        if (!string.Equals(username, "erp_app", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The runtime must connect as erp_app. Owner, migration, platform-administration " +
                "and superuser credentials are prohibited (ADR-0012).");
        }

        if (MaximumPoolSize is < 1 or > 500)
        {
            throw new InvalidOperationException("Database:MaximumPoolSize must be between 1 and 500.");
        }

        if (CommandTimeoutSeconds is < 1 or > 600)
        {
            throw new InvalidOperationException("Database:CommandTimeoutSeconds must be between 1 and 600.");
        }
    }
}
