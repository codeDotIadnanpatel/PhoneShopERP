using Npgsql;
using PhoneShopErp.Modules.IdentityAccess.Application;
using PhoneShopErp.Persistence;
using PhoneShopErp.Platform.Abstractions;
using PhoneShopErp.Platform.Audit;
using PhoneShopErp.Platform.Errors;
using PhoneShopErp.Platform.Security;
using PhoneShopErp.Platform.Tenancy;

namespace PhoneShopErp.Modules.IdentityAccess.Infrastructure;

/// <summary>
/// SQL-first identity adapter.
///
/// Every read that runs before a tenant is selected uses the pre-tenant context
/// (<c>app.subject</c> and <c>app.user_id</c>) and the two approved pre-tenant read
/// paths: the caller's own <c>identity.user</c>, own <c>identity.membership</c>,
/// own <c>identity.session</c> by provider reference, and the tenant rows those
/// memberships reference. Nothing here trusts a token claim for tenant, role or
/// permission (ADR-0013).
///
/// Session resolution happens in one transaction: the session row is proven to
/// belong to the user, then tenant context is applied from that row (never from
/// the request) and authorisation data is read under RLS.
/// </summary>
public sealed class PostgresIdentityService : IIdentityService
{
    private readonly IPreTenantUnitOfWorkFactory preTenantUnitOfWorkFactory;
    private readonly ITenantUnitOfWorkFactory tenantUnitOfWorkFactory;
    private readonly IAuditStore auditStore;
    private readonly IIdGenerator idGenerator;
    private readonly IClock clock;
    private readonly SessionPolicy sessionPolicy;

    public PostgresIdentityService(
        IPreTenantUnitOfWorkFactory preTenantUnitOfWorkFactory,
        ITenantUnitOfWorkFactory tenantUnitOfWorkFactory,
        IAuditStore auditStore,
        IIdGenerator idGenerator,
        IClock clock,
        SessionPolicy sessionPolicy)
    {
        this.preTenantUnitOfWorkFactory = preTenantUnitOfWorkFactory;
        this.tenantUnitOfWorkFactory = tenantUnitOfWorkFactory;
        this.auditStore = auditStore;
        this.idGenerator = idGenerator;
        this.clock = clock;
        this.sessionPolicy = sessionPolicy;
    }

    public async Task<Guid> EnsureUserAsync(
        AuthenticatedPrincipal principal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var subject = RequireSubject(principal.Subject);

        await using var unitOfWork = await preTenantUnitOfWorkFactory
            .BeginAsync(PreTenantContext.ForSubject(subject), cancellationToken)
            .ConfigureAwait(false);

        var user = await EnsureUserRowAsync(unitOfWork, principal, subject, cancellationToken)
            .ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return user.Id;
    }

    public async Task<IReadOnlyList<MembershipSummary>> GetMyMembershipsAsync(
        AuthenticatedPrincipal principal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var userId = await FindUserIdAsync(principal, cancellationToken).ConfigureAwait(false);
        if (userId is not { } resolvedUserId)
        {
            // An unknown subject has no ERP user yet and therefore no memberships.
            // This is not an anonymous path: every other read still requires a
            // provisioned user, membership and tenant context.
            return [];
        }

        await using var unitOfWork = await preTenantUnitOfWorkFactory
            .BeginAsync(
                PreTenantContext.ForUser(principal.Subject, resolvedUserId, principal.ProviderSessionReference),
                cancellationToken)
            .ConfigureAwait(false);

        const string sql = """
            SELECT m.id, m.tenant_id, t.slug, t.display_name, m.status, t.status
            FROM identity.membership m
            JOIN tenancy.tenant t ON t.id = m.tenant_id
            WHERE m.user_id = @user_id AND m.status <> 'removed'
            ORDER BY t.display_name, t.id;
            """;

        var memberships = new List<MembershipSummary>();
        await using (var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction))
        {
            command.Parameters.AddWithValue("user_id", resolvedUserId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                memberships.Add(new MembershipSummary(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5)));
            }
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return memberships;
    }

    public async Task<ResolvedSession?> ResolveAsync(
        AuthenticatedPrincipal principal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (string.IsNullOrWhiteSpace(principal.ProviderSessionReference))
        {
            return null;
        }

        var userId = await FindUserIdAsync(principal, cancellationToken).ConfigureAwait(false);
        if (userId is null)
        {
            return null;
        }

        await using var unitOfWork = await preTenantUnitOfWorkFactory
            .BeginAsync(
                PreTenantContext.ForUser(
                    principal.Subject,
                    userId.Value,
                    principal.ProviderSessionReference),
                cancellationToken)
            .ConfigureAwait(false);

        var now = clock.UtcNow;
        var session = await ReadActiveSessionAsync(
            unitOfWork,
            principal.ProviderSessionReference,
            now,
            cancellationToken).ConfigureAwait(false);

        if (session is null)
        {
            await unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        // The tenant and membership come from the stored session row. Applying the
        // context here is safe precisely because that row was resolved from the
        // validated subject and provider session reference, not from a claim.
        await unitOfWork
            .ApplyTenantContextAsync(session.TenantId, userId.Value, cancellationToken)
            .ConfigureAwait(false);

        var (permissions, assignments) = await ReadAuthorisationAsync(
            unitOfWork,
            session.MembershipId,
            cancellationToken).ConfigureAwait(false);

        var platformPermissions = await ReadPlatformPermissionsAsync(
            unitOfWork,
            userId.Value,
            cancellationToken).ConfigureAwait(false);

        await TouchSessionAsync(unitOfWork, session.SessionId, now, cancellationToken)
            .ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return ResolvedSession.Create(
            session.SessionId,
            session.TenantId,
            userId.Value,
            session.MembershipId,
            session.PermissionVersion,
            session.DeviceId,
            assignments.TenantWide,
            permissions,
            platformPermissions,
            assignments.BranchIds,
            assignments.LegalEntityIds,
            assignments.StockLocationIds);
    }

    public async Task<SessionSummary> CreateSessionAsync(
        AuthenticatedPrincipal principal,
        CreateSessionRequest request,
        SessionRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(metadata);

        if (request.TenantId == Guid.Empty)
        {
            throw new PlatformException(
                ErrorCodes.ValidationFailed,
                "A tenant identifier is required to start a session.",
                400);
        }

        var reference = RequireSessionReference(principal.ProviderSessionReference);
        var userId = await EnsureUserAsync(principal, cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;

        await using var unitOfWork = await preTenantUnitOfWorkFactory
            .BeginAsync(
                PreTenantContext.ForUser(principal.Subject, userId, reference),
                cancellationToken)
            .ConfigureAwait(false);

        var membership = await ReadMembershipForSessionAsync(
            unitOfWork,
            request.TenantId,
            userId,
            cancellationToken).ConfigureAwait(false);

        if (membership is null)
        {
            throw new PlatformException(
                ErrorCodes.NotFound,
                "The tenant does not exist or the user is not a member of it.",
                404);
        }

        if (!string.Equals(membership.MembershipStatus, "active", StringComparison.Ordinal))
        {
            throw new PlatformException(
                ErrorCodes.Forbidden,
                "The membership is not active.",
                403);
        }

        if (!string.Equals(membership.TenantStatus, "active", StringComparison.Ordinal))
        {
            throw new PlatformException(
                ErrorCodes.Forbidden,
                "The tenant is not active.",
                403);
        }

        var existing = await ReadActiveSessionAsync(unitOfWork, reference, now, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null && existing.TenantId == request.TenantId)
        {
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SessionSummary(
                existing.SessionId,
                existing.TenantId,
                existing.MembershipId,
                existing.PermissionVersion,
                existing.IssuedAt,
                existing.IdleExpiresAt,
                existing.AbsoluteExpiresAt);
        }

        if (existing is not null)
        {
            // Switching tenant means a new session. Revoke the old one in its own
            // tenant context first, then create the new session. The lock ordering
            // is intentional: one tenant context at a time.
            await unitOfWork
                .ApplyTenantContextAsync(existing.TenantId, userId, cancellationToken)
                .ConfigureAwait(false);

            await RevokeAsync(unitOfWork, existing.SessionId, "tenant_switch", now, cancellationToken)
                .ConfigureAwait(false);

            await auditStore.AppendAsync(
                unitOfWork,
                new AuditEvent(
                    idGenerator.NewId(),
                    existing.TenantId,
                    now,
                    AuditActorTypes.User,
                    userId,
                    AuditActions.SessionTenantSwitched,
                    AuditOutcomes.Success,
                    "session",
                    existing.SessionId,
                    null,
                    null,
                    null),
                cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork
            .ApplyTenantContextAsync(request.TenantId, userId, cancellationToken)
            .ConfigureAwait(false);

        var sessionId = idGenerator.NewId();
        var idleExpiresAt = now + sessionPolicy.IdleTimeout;
        var absoluteExpiresAt = now + sessionPolicy.AbsoluteTimeout;

        const string insert = """
            INSERT INTO identity.session (
                id, tenant_id, user_id, membership_id, device_id, permission_version,
                provider_session_reference, fingerprint_hash, issued_at, last_seen_at,
                idle_expires_at, absolute_expires_at)
            VALUES (
                @id, @tenant_id, @user_id, @membership_id, NULL, @permission_version,
                @provider_session_reference, @fingerprint, @now, @now,
                @idle_expires_at, @absolute_expires_at)
            RETURNING id, tenant_id, membership_id, permission_version,
                      issued_at, idle_expires_at, absolute_expires_at;
            """;

        SessionSummary summary;
        await using (var command = new NpgsqlCommand(insert, unitOfWork.Connection, unitOfWork.Transaction))
        {
            command.Parameters.AddWithValue("id", sessionId);
            command.Parameters.AddWithValue("tenant_id", request.TenantId);
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("membership_id", membership.MembershipId);
            command.Parameters.AddWithValue("permission_version", membership.PermissionVersion);
            command.Parameters.AddWithValue("provider_session_reference", reference);
            command.Parameters.AddWithValue("fingerprint", metadata.BindingFingerprint());
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("idle_expires_at", idleExpiresAt);
            command.Parameters.AddWithValue("absolute_expires_at", absoluteExpiresAt);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new PlatformException(
                    ErrorCodes.InternalError,
                    "The application session could not be created.",
                    500);
            }

            summary = new SessionSummary(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetInt32(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetFieldValue<DateTimeOffset>(6));
        }

        await auditStore.AppendAsync(
            unitOfWork,
            new AuditEvent(
                idGenerator.NewId(),
                request.TenantId,
                now,
                AuditActorTypes.User,
                userId,
                AuditActions.SessionCreated,
                AuditOutcomes.Success,
                "session",
                sessionId,
                null,
                null,
                null),
            cancellationToken).ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return summary;
    }

    public async Task RevokeSessionAsync(
        Guid sessionId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty)
        {
            throw new PlatformException(
                ErrorCodes.ValidationFailed,
                "A session identifier is required.",
                400);
        }

        var context = RequireContext();

        // Sign-out of the caller's own session needs no permission. Revoking any
        // other session in the tenant requires session.revoke.
        if (sessionId != context.SessionId)
        {
            PermissionChecker.Demand(context, "session.revoke");
        }

        var validatedReason = RequireReason(reason);
        var now = clock.UtcNow;

        await using var unitOfWork = await tenantUnitOfWorkFactory
            .BeginAsync(context, cancellationToken)
            .ConfigureAwait(false);

        var revoked = await RevokeAsync(unitOfWork, sessionId, validatedReason, now, cancellationToken)
            .ConfigureAwait(false);

        if (!revoked)
        {
            throw new PlatformException(
                ErrorCodes.NotFound,
                "The session does not exist or was already revoked.",
                404);
        }

        await auditStore.AppendAsync(
            unitOfWork,
            new AuditEvent(
                idGenerator.NewId(),
                context.TenantId,
                now,
                AuditActorTypes.User,
                context.UserId,
                AuditActions.SessionRevoked,
                AuditOutcomes.Success,
                "session",
                sessionId,
                null,
                validatedReason,
                null),
            cancellationToken).ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SessionSummary>> GetMySessionsAsync(
        CancellationToken cancellationToken)
    {
        var context = RequireContext();

        await using var unitOfWork = await tenantUnitOfWorkFactory
            .BeginAsync(context, cancellationToken)
            .ConfigureAwait(false);

        const string sql = """
            SELECT id, tenant_id, membership_id, permission_version,
                   issued_at, idle_expires_at, absolute_expires_at
            FROM identity.session
            WHERE user_id = @user_id
              AND tenant_id = platform.current_tenant_id()
              AND revoked_at IS NULL
            ORDER BY issued_at DESC;
            """;

        var sessions = new List<SessionSummary>();
        await using (var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction))
        {
            command.Parameters.AddWithValue("user_id", context.UserId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                sessions.Add(new SessionSummary(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetInt32(3),
                    reader.GetFieldValue<DateTimeOffset>(4),
                    reader.GetFieldValue<DateTimeOffset>(5),
                    reader.GetFieldValue<DateTimeOffset>(6)));
            }
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return sessions;
    }

    private static TenantContext RequireContext() =>
        TenantContextAccessor.Current
        ?? throw new PlatformException(
            ErrorCodes.TenantContextMissing,
            "A tenant context is required for this operation.",
            400);

    private static string RequireSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 255)
        {
            throw Unauthenticated();
        }

        return subject;
    }

    private static string RequireSessionReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 255)
        {
            throw Unauthenticated();
        }

        return reference;
    }

    private static string RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            throw new PlatformException(
                ErrorCodes.ValidationFailed,
                "A revocation reason of 1-500 characters is required.",
                400);
        }

        return reason;
    }

    private static PlatformException Unauthenticated() =>
        new(
            ErrorCodes.Unauthenticated,
            "The authenticated subject could not be resolved.",
            401);

    private async Task<Guid?> FindUserIdAsync(
        AuthenticatedPrincipal principal,
        CancellationToken cancellationToken)
    {
        var subject = RequireSubject(principal.Subject);

        await using var unitOfWork = await preTenantUnitOfWorkFactory
            .BeginAsync(PreTenantContext.ForSubject(subject), cancellationToken)
            .ConfigureAwait(false);

        const string sql = """
            SELECT id, status
            FROM identity.user
            WHERE subject = platform.current_subject();
            """;

        Guid? userId = null;
        string? status = null;

        await using (var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction))
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                userId = reader.GetGuid(0);
                status = reader.GetString(1);
            }
        }

        await unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);

        if (userId is null || !string.Equals(status, "active", StringComparison.Ordinal))
        {
            return null;
        }

        return userId;
    }

    private async Task<(Guid Id, string Status)> EnsureUserRowAsync(
        IUnitOfWork unitOfWork,
        AuthenticatedPrincipal principal,
        string subject,
        CancellationToken cancellationToken)
    {
        const string insert = """
            INSERT INTO identity.user (id, subject, email, display_name, status)
            VALUES (@id, @subject, @email, @display_name, 'active')
            ON CONFLICT (subject) DO NOTHING;
            """;

        await using (var command = new NpgsqlCommand(insert, unitOfWork.Connection, unitOfWork.Transaction))
        {
            command.Parameters.AddWithValue("id", idGenerator.NewId());
            command.Parameters.AddWithValue("subject", subject);
            command.Parameters.AddWithValue("email", (object?)principal.Email ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "display_name",
                string.IsNullOrWhiteSpace(principal.DisplayName) ? subject : principal.DisplayName);

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        const string select = """
            SELECT id, status
            FROM identity.user
            WHERE subject = platform.current_subject();
            """;

        await using var read = new NpgsqlCommand(select, unitOfWork.Connection, unitOfWork.Transaction);
        await using var reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw Unauthenticated();
        }

        return (reader.GetGuid(0), reader.GetString(1));
    }

    private static async Task<StoredSession?> ReadActiveSessionAsync(
        IUnitOfWork unitOfWork,
        string reference,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT s.id, s.tenant_id, s.membership_id, s.permission_version, s.device_id,
                   s.issued_at, s.idle_expires_at, s.absolute_expires_at,
                   m.status, u.status
            FROM identity.session s
            JOIN identity.membership m ON m.tenant_id = s.tenant_id AND m.id = s.membership_id
            JOIN identity.user u ON u.id = s.user_id
            WHERE s.provider_session_reference = @reference
              AND s.user_id = platform.current_user_id()
              AND s.revoked_at IS NULL
            ORDER BY s.issued_at DESC
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("reference", reference);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var session = new StoredSession(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetFieldValue<DateTimeOffset>(7));

        var membershipStatus = reader.GetString(8);
        var userStatus = reader.GetString(9);

        var expired = now >= session.IdleExpiresAt || now >= session.AbsoluteExpiresAt;

        if (expired
            || !string.Equals(membershipStatus, "active", StringComparison.Ordinal)
            || !string.Equals(userStatus, "active", StringComparison.Ordinal))
        {
            return null;
        }

        return session;
    }

    private static async Task<StoredMembership?> ReadMembershipForSessionAsync(
        IUnitOfWork unitOfWork,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT m.id, m.status, m.permission_version, t.status
            FROM identity.membership m
            JOIN tenancy.tenant t ON t.id = m.tenant_id
            WHERE m.tenant_id = @tenant_id
              AND m.user_id = @user_id
              AND m.status <> 'removed';
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new StoredMembership(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3));
    }

    private static async Task<(IReadOnlyList<string> Permissions, ScopeAssignments Assignments)>
        ReadAuthorisationAsync(
            IUnitOfWork unitOfWork,
            Guid membershipId,
            CancellationToken cancellationToken)
    {
        // Only tenant-scoped permissions can come from a tenant role. A platform
        // permission seeded in identity.permission can never leak in through this
        // path, even if a role_permission row were inserted by mistake.
        const string permissionsSql = """
            SELECT DISTINCT rp.permission_code
            FROM identity.role_assignment ra
            JOIN identity.role r
                ON r.tenant_id = ra.tenant_id AND r.id = ra.role_id AND r.is_active
            JOIN identity.role_permission rp
                ON rp.tenant_id = ra.tenant_id AND rp.role_id = ra.role_id
            JOIN identity.permission p
                ON p.code = rp.permission_code AND p.scope = 'tenant' AND p.is_active
            WHERE ra.membership_id = @membership_id
              AND ra.revoked_at IS NULL
            ORDER BY rp.permission_code;
            """;

        var permissions = new List<string>();
        await using (var command = new NpgsqlCommand(
            permissionsSql,
            unitOfWork.Connection,
            unitOfWork.Transaction))
        {
            command.Parameters.AddWithValue("membership_id", membershipId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                permissions.Add(reader.GetString(0));
            }
        }

        const string assignmentsSql = """
            SELECT ra.scope_type, ra.scope_id
            FROM identity.role_assignment ra
            JOIN identity.role r
                ON r.tenant_id = ra.tenant_id AND r.id = ra.role_id AND r.is_active
            WHERE ra.membership_id = @membership_id
              AND ra.revoked_at IS NULL;
            """;

        var tenantWide = false;
        var branchIds = new List<Guid>();
        var legalEntityIds = new List<Guid>();
        var stockLocationIds = new List<Guid>();

        await using (var command = new NpgsqlCommand(
            assignmentsSql,
            unitOfWork.Connection,
            unitOfWork.Transaction))
        {
            command.Parameters.AddWithValue("membership_id", membershipId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var scopeType = reader.GetString(0);
                var scopeId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1);

                switch (scopeType)
                {
                    case "tenant":
                        tenantWide = true;
                        break;
                    case "branch":
                        if (scopeId is { } branchId)
                        {
                            branchIds.Add(branchId);
                        }

                        break;
                    case "legal_entity":
                        if (scopeId is { } legalEntityId)
                        {
                            legalEntityIds.Add(legalEntityId);
                        }

                        break;
                    case "stock_location":
                        if (scopeId is { } stockLocationId)
                        {
                            stockLocationIds.Add(stockLocationId);
                        }

                        break;
                    default:
                        // A scope type this build does not understand is ignored, so
                        // it can only narrow access, never widen it.
                        break;
                }
            }
        }

        return (
            permissions,
            new ScopeAssignments(tenantWide, branchIds, legalEntityIds, stockLocationIds));
    }

    private static async Task<IReadOnlyList<string>> ReadPlatformPermissionsAsync(
        IUnitOfWork unitOfWork,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT g.permission_code
            FROM identity.platform_grant g
            JOIN identity.permission p
                ON p.code = g.permission_code AND p.scope = 'platform' AND p.is_active
            WHERE g.user_id = @user_id AND g.is_active
            ORDER BY g.permission_code;
            """;

        var permissions = new List<string>();
        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            permissions.Add(reader.GetString(0));
        }

        return permissions;
    }

    private async Task TouchSessionAsync(
        IUnitOfWork unitOfWork,
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Sliding idle window, capped by the absolute expiry. The row-version
        // trigger records the write.
        const string sql = """
            UPDATE identity.session
            SET last_seen_at = @now,
                idle_expires_at = LEAST(
                    @now + @idle_timeout,
                    absolute_expires_at)
            WHERE id = @session_id
              AND tenant_id = platform.current_tenant_id()
              AND revoked_at IS NULL;
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("idle_timeout", sessionPolicy.IdleTimeout);
        command.Parameters.AddWithValue("session_id", sessionId);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> RevokeAsync(
        IUnitOfWork unitOfWork,
        Guid sessionId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE identity.session
            SET revoked_at = @now, revoke_reason = @reason
            WHERE id = @session_id
              AND tenant_id = platform.current_tenant_id()
              AND revoked_at IS NULL;
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("session_id", sessionId);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    private sealed record StoredSession(
        Guid SessionId,
        Guid TenantId,
        Guid MembershipId,
        int PermissionVersion,
        Guid? DeviceId,
        DateTimeOffset IssuedAt,
        DateTimeOffset IdleExpiresAt,
        DateTimeOffset AbsoluteExpiresAt);

    private sealed record StoredMembership(
        Guid MembershipId,
        string MembershipStatus,
        int PermissionVersion,
        string TenantStatus);

    private sealed record ScopeAssignments(
        bool TenantWide,
        IReadOnlyList<Guid> BranchIds,
        IReadOnlyList<Guid> LegalEntityIds,
        IReadOnlyList<Guid> StockLocationIds);
}
