using Npgsql;
using NpgsqlTypes;
using PhoneShopErp.Platform.Audit;
using PhoneShopErp.Platform.Security;

namespace PhoneShopErp.Persistence;

public interface IAuditStore
{
    Task AppendAsync(IUnitOfWork unitOfWork, AuditEvent auditEvent, CancellationToken cancellationToken);

    Task AppendSecurityEventAsync(
        IUnitOfWork unitOfWork,
        SecurityEvent securityEvent,
        CancellationToken cancellationToken);
}

/// <summary>
/// Writes audit and security events inside the caller's transaction. Both
/// <c>Detail</c> payloads are redacted here, at the persistence boundary, so an
/// alternate caller cannot bypass the control. Callers should still avoid
/// collecting credential material in the first place.
/// </summary>
public sealed class PostgresAuditStore : IAuditStore
{
    public async Task AppendAsync(
        IUnitOfWork unitOfWork,
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(auditEvent);

        const string sql = """
            INSERT INTO platform.audit_event (
                id, tenant_id, occurred_at, actor_type, actor_user_id, action, outcome,
                target_type, target_id, correlation_id, reason, detail)
            VALUES (
                @id, @tenant_id, @occurred_at, @actor_type, @actor_user_id, @action, @outcome,
                @target_type, @target_id, @correlation_id, @reason, @detail);
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("id", auditEvent.Id);
        command.Parameters.AddWithValue("tenant_id", auditEvent.TenantId);
        command.Parameters.AddWithValue("occurred_at", auditEvent.OccurredAt);
        command.Parameters.AddWithValue("actor_type", auditEvent.ActorType);
        command.Parameters.AddWithValue("actor_user_id", (object?)auditEvent.ActorUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("action", auditEvent.Action);
        command.Parameters.AddWithValue("outcome", auditEvent.Outcome);
        command.Parameters.AddWithValue("target_type", (object?)auditEvent.TargetType ?? DBNull.Value);
        command.Parameters.AddWithValue("target_id", (object?)auditEvent.TargetId ?? DBNull.Value);
        command.Parameters.AddWithValue("correlation_id", (object?)auditEvent.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("reason", (object?)auditEvent.Reason ?? DBNull.Value);

        var detail = command.Parameters.Add("detail", NpgsqlDbType.Jsonb);
        detail.Value = auditEvent.Detail is null
            ? DBNull.Value
            : SensitiveDataRedactor.RedactJson(auditEvent.Detail.RootElement);

        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendSecurityEventAsync(
        IUnitOfWork unitOfWork,
        SecurityEvent securityEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(securityEvent);

        const string sql = """
            INSERT INTO platform.security_event (
                id, occurred_at, category, severity, outcome, actor_reference,
                correlation_id, detail)
            VALUES (
                @id, @occurred_at, @category, @severity, @outcome, @actor_reference,
                @correlation_id, @detail);
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("id", securityEvent.Id);
        command.Parameters.AddWithValue("occurred_at", securityEvent.OccurredAt);
        command.Parameters.AddWithValue("category", securityEvent.Category);
        command.Parameters.AddWithValue("severity", securityEvent.Severity);
        command.Parameters.AddWithValue("outcome", securityEvent.Outcome);
        command.Parameters.AddWithValue("actor_reference", (object?)securityEvent.ActorReference ?? DBNull.Value);
        command.Parameters.AddWithValue("correlation_id", (object?)securityEvent.CorrelationId ?? DBNull.Value);

        var detail = command.Parameters.Add("detail", NpgsqlDbType.Jsonb);
        detail.Value = securityEvent.Detail is null
            ? DBNull.Value
            : SensitiveDataRedactor.RedactJson(securityEvent.Detail.RootElement);

        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
