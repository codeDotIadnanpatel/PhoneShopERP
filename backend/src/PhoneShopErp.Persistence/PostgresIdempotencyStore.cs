using Npgsql;
using PhoneShopErp.Platform.Abstractions;
using PhoneShopErp.Platform.Errors;

namespace PhoneShopErp.Persistence;

public sealed record IdempotencyReservation(
    string Scope,
    string Key,
    bool IsReplay,
    bool IsInProgress,
    int? ResponseStatus,
    string? ResponseBody,
    Guid? ResourceId);

public interface IIdempotencyStore
{
    Task<IdempotencyReservation> ReserveAsync(
        IUnitOfWork unitOfWork,
        string scope,
        string key,
        string requestHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        IUnitOfWork unitOfWork,
        string scope,
        string key,
        int responseStatus,
        string responseBody,
        Guid? resourceId,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        IUnitOfWork unitOfWork,
        string scope,
        string key,
        CancellationToken cancellationToken);
}

/// <summary>
/// Tenant-scoped idempotency. The unique key is
/// <c>(tenant_id, scope, idempotency_key)</c>, so one tenant can never observe or
/// replay another tenant's command.
/// </summary>
public sealed class PostgresIdempotencyStore : IIdempotencyStore
{
    private readonly IIdGenerator idGenerator;
    private readonly IClock clock;

    public PostgresIdempotencyStore(IIdGenerator idGenerator, IClock clock)
    {
        this.idGenerator = idGenerator;
        this.clock = clock;
    }

    public async Task<IdempotencyReservation> ReserveAsync(
        IUnitOfWork unitOfWork,
        string scope,
        string key,
        string requestHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        const string insert = """
            INSERT INTO platform.idempotency_record (
                id, tenant_id, scope, idempotency_key, request_hash, status, expires_at)
            VALUES (
                @id, platform.current_tenant_id(), @scope, @key, @hash, 'in_progress', @expires_at)
            ON CONFLICT (tenant_id, scope, idempotency_key) DO NOTHING
            RETURNING id;
            """;

        await using (var command = new NpgsqlCommand(insert, unitOfWork.Connection, unitOfWork.Transaction))
        {
            command.Parameters.AddWithValue("id", idGenerator.NewId());
            command.Parameters.AddWithValue("scope", scope);
            command.Parameters.AddWithValue("key", key);
            command.Parameters.AddWithValue("hash", requestHash);
            command.Parameters.AddWithValue("expires_at", expiresAt);

            var inserted = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (inserted is not null and not DBNull)
            {
                return new IdempotencyReservation(scope, key, false, false, null, null, null);
            }
        }

        const string select = """
            SELECT request_hash, status, response_status, response_body::text, resource_id
            FROM platform.idempotency_record
            WHERE tenant_id = platform.current_tenant_id()
              AND scope = @scope
              AND idempotency_key = @key;
            """;

        await using var read = new NpgsqlCommand(select, unitOfWork.Connection, unitOfWork.Transaction);
        read.Parameters.AddWithValue("scope", scope);
        read.Parameters.AddWithValue("key", key);

        await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // The conflicting row disappeared between the insert and the read
            // (for example an expired record was deleted). Treat it as a fresh
            // reservation rather than silently reusing an unknown outcome.
            throw new PlatformException(
                ErrorCodes.IdempotencyInProgress,
                "The idempotency key is being processed. Retry shortly.",
                409);
        }

        var storedHash = reader.GetString(0);
        var status = reader.GetString(1);

        if (!string.Equals(storedHash, requestHash, StringComparison.Ordinal))
        {
            throw new PlatformException(
                ErrorCodes.IdempotencyKeyReused,
                "The idempotency key was already used with a different request.",
                409);
        }

        if (string.Equals(status, "in_progress", StringComparison.Ordinal))
        {
            return new IdempotencyReservation(scope, key, false, true, null, null, null);
        }

        return new IdempotencyReservation(
            scope,
            key,
            true,
            false,
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4));
    }

    public async Task CompleteAsync(
        IUnitOfWork unitOfWork,
        string scope,
        string key,
        int responseStatus,
        string responseBody,
        Guid? resourceId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE platform.idempotency_record
            SET status = 'completed',
                response_status = @response_status,
                response_body = @response_body::jsonb,
                resource_id = @resource_id
            WHERE tenant_id = platform.current_tenant_id()
              AND scope = @scope
              AND idempotency_key = @key
              AND status = 'in_progress';
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("response_status", responseStatus);
        command.Parameters.AddWithValue("response_body", responseBody);
        command.Parameters.AddWithValue("resource_id", (object?)resourceId ?? DBNull.Value);
        command.Parameters.AddWithValue("scope", scope);
        command.Parameters.AddWithValue("key", key);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (affected != 1)
        {
            throw new PlatformException(
                ErrorCodes.IdempotencyInProgress,
                "The idempotency record is no longer in progress.",
                409);
        }
    }

    public async Task ReleaseAsync(
        IUnitOfWork unitOfWork,
        string scope,
        string key,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM platform.idempotency_record
            WHERE tenant_id = platform.current_tenant_id()
              AND scope = @scope
              AND idempotency_key = @key
              AND status = 'in_progress';
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("scope", scope);
        command.Parameters.AddWithValue("key", key);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Pre-tenant idempotency for tenant creation only. It stores an opaque key, a
/// request fingerprint and the generated tenant identifier. No tenant business
/// payload is kept and no unbounded read is exposed.
/// </summary>
public interface IOnboardingRequestStore
{
    Task<OnboardingReservation> ReserveAsync(
        IUnitOfWork unitOfWork,
        string key,
        string requestHash,
        Guid candidateTenantId,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        IUnitOfWork unitOfWork,
        string key,
        Guid tenantId,
        CancellationToken cancellationToken);

    Task ReleaseAsync(IUnitOfWork unitOfWork, string key, CancellationToken cancellationToken);
}

public sealed record OnboardingReservation(
    string Key,
    bool IsReplay,
    bool IsInProgress,
    Guid TenantId);

public sealed class PostgresOnboardingRequestStore : IOnboardingRequestStore
{
    private const string Scope = "tenant.create";

    public async Task<OnboardingReservation> ReserveAsync(
        IUnitOfWork unitOfWork,
        string key,
        string requestHash,
        Guid candidateTenantId,
        CancellationToken cancellationToken)
    {
        const string insert = """
            INSERT INTO platform.onboarding_request (
                requested_by_user_id, idempotency_key, request_hash, scope, status, created_tenant_id)
            VALUES (
                platform.current_user_id(), @key, @hash, 'tenant.create', 'in_progress', @tenant_id)
            ON CONFLICT (requested_by_user_id, idempotency_key) DO NOTHING
            RETURNING idempotency_key;
            """;

        await using (var command = new NpgsqlCommand(insert, unitOfWork.Connection, unitOfWork.Transaction))
        {
            command.Parameters.AddWithValue("key", key);
            command.Parameters.AddWithValue("hash", requestHash);
            command.Parameters.AddWithValue("tenant_id", candidateTenantId);

            var inserted = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (inserted is not null and not DBNull)
            {
                return new OnboardingReservation(key, false, false, candidateTenantId);
            }
        }

        const string select = """
            SELECT request_hash, status, created_tenant_id
            FROM platform.onboarding_request
            WHERE idempotency_key = @key
              AND requested_by_user_id = platform.current_user_id()
              AND scope = 'tenant.create';
            """;

        await using var read = new NpgsqlCommand(select, unitOfWork.Connection, unitOfWork.Transaction);
        read.Parameters.AddWithValue("key", key);

        await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new PlatformException(
                ErrorCodes.IdempotencyInProgress,
                "The onboarding request is being processed. Retry shortly.",
                409);
        }

        if (!string.Equals(reader.GetString(0), requestHash, StringComparison.Ordinal))
        {
            throw new PlatformException(
                ErrorCodes.IdempotencyKeyReused,
                "The idempotency key was already used with a different request.",
                409);
        }

        var status = reader.GetString(1);
        var tenantId = reader.GetGuid(2);

        return new OnboardingReservation(
            key,
            IsReplay: string.Equals(status, "completed", StringComparison.Ordinal),
            IsInProgress: string.Equals(status, "in_progress", StringComparison.Ordinal),
            tenantId);
    }

    public async Task CompleteAsync(
        IUnitOfWork unitOfWork,
        string key,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE platform.onboarding_request
            SET status = 'completed', created_tenant_id = @tenant_id
            WHERE idempotency_key = @key AND scope = 'tenant.create' AND status = 'in_progress'
              AND requested_by_user_id = platform.current_user_id();
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseAsync(
        IUnitOfWork unitOfWork,
        string key,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM platform.onboarding_request
            WHERE idempotency_key = @key AND scope = 'tenant.create' AND status = 'in_progress'
              AND requested_by_user_id = platform.current_user_id();
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("key", key);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
