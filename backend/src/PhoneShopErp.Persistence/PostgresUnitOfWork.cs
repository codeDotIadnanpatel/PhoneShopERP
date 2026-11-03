using System.Data;
using Npgsql;
using PhoneShopErp.Platform.Tenancy;

namespace PhoneShopErp.Persistence;

internal sealed class PostgresUnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;

    private PostgresUnitOfWork(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        this.connection = connection;
        this.transaction = transaction;
    }

    public NpgsqlConnection Connection => connection;

    public NpgsqlTransaction Transaction => transaction;

    public bool IsCompleted { get; private set; }

    public static async Task<IUnitOfWork> BeginTenantAsync(
        IDbConnectionFactory connectionFactory,
        TenantContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var unitOfWork = await BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SetContextAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [PostgresContextSettings.TenantId] = context.TenantId.ToString("D"),
                [PostgresContextSettings.UserId] = context.UserId.ToString("D"),
                [PostgresContextSettings.SessionId] = context.SessionId.ToString("D"),
            },
            cancellationToken).ConfigureAwait(false);

        return unitOfWork;
    }

    public static async Task<IUnitOfWork> BeginPreTenantAsync(
        IDbConnectionFactory connectionFactory,
        PreTenantContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var unitOfWork = await BeginAsync(connectionFactory, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SetContextAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [PostgresContextSettings.TenantId] = null,
                [PostgresContextSettings.Subject] = context.Subject,
                [PostgresContextSettings.UserId] = context.UserId?.ToString("D"),
                [PostgresContextSettings.SessionReference] = context.SessionReference,
            },
            cancellationToken).ConfigureAwait(false);

        return unitOfWork;
    }

    private static async Task<PostgresUnitOfWork> BeginAsync(
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken)
    {
        var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = await connection
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                .ConfigureAwait(false);

            return new PostgresUnitOfWork(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task SetContextAsync(
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken)
    {
        foreach (var (name, value) in values)
        {
            await using var command = new NpgsqlCommand(
                "SELECT set_config(@name, @value, true);",
                connection,
                transaction);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("value", value ?? string.Empty);
            _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ApplyTenantContextAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (IsCompleted)
        {
            throw new InvalidOperationException("The unit of work has already completed.");
        }

        await SetContextAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [PostgresContextSettings.TenantId] = tenantId.ToString("D"),
                [PostgresContextSettings.UserId] = userId.ToString("D"),
                [PostgresContextSettings.SessionId] = null,
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyPreTenantUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (IsCompleted)
        {
            throw new InvalidOperationException("The unit of work has already completed.");
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));
        }

        await SetContextAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [PostgresContextSettings.UserId] = userId.ToString("D"),
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (IsCompleted)
        {
            throw new InvalidOperationException("The unit of work has already completed.");
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        IsCompleted = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (IsCompleted)
        {
            return;
        }

        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        IsCompleted = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!IsCompleted)
            {
                // A handler that throws loses the whole transaction. Rolling back
                // here guarantees the connection is returned clean.
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}

public sealed class PostgresTenantUnitOfWorkFactory : ITenantUnitOfWorkFactory
{
    private readonly IDbConnectionFactory connectionFactory;

    public PostgresTenantUnitOfWorkFactory(IDbConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public Task<IUnitOfWork> BeginAsync(TenantContext context, CancellationToken cancellationToken) =>
        PostgresUnitOfWork.BeginTenantAsync(connectionFactory, context, cancellationToken);
}

public sealed class PostgresPreTenantUnitOfWorkFactory : IPreTenantUnitOfWorkFactory
{
    private readonly IDbConnectionFactory connectionFactory;

    public PostgresPreTenantUnitOfWorkFactory(IDbConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public Task<IUnitOfWork> BeginAsync(PreTenantContext context, CancellationToken cancellationToken) =>
        PostgresUnitOfWork.BeginPreTenantAsync(connectionFactory, context, cancellationToken);
}
