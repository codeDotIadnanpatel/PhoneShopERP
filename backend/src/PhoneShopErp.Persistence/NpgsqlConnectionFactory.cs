using Npgsql;

namespace PhoneShopErp.Persistence;

public interface IDbConnectionFactory
{
    Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken);
}

public sealed class NpgsqlConnectionFactory : IDbConnectionFactory, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;

    public NpgsqlConnectionFactory(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var builder = new NpgsqlDataSourceBuilder(options.ConnectionString);
        builder.ConnectionStringBuilder.ApplicationName = "PhoneShopErp";
        builder.ConnectionStringBuilder.MaxPoolSize = options.MaximumPoolSize;
        builder.ConnectionStringBuilder.Timeout = options.CommandTimeoutSeconds;

        dataSource = builder.Build();
    }

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) =>
        await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    public async ValueTask DisposeAsync() => await dataSource.DisposeAsync().ConfigureAwait(false);
}
