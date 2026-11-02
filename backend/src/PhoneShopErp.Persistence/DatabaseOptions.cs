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
    /// migration or superuser credential. A runtime connection must be erp_app.
    /// PGHOST/PGUSER style environment configuration is allowed, but if a user is
    /// named in the connection string it is validated.
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

        if (!string.IsNullOrWhiteSpace(builder.Username))
        {
            var username = builder.Username.Trim();
            if (username is "erp_owner" or "erp_migrator" or "postgres")
            {
                throw new InvalidOperationException(
                    "The runtime must not connect as an owner, migration or superuser role. Use erp_app.");
            }
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
