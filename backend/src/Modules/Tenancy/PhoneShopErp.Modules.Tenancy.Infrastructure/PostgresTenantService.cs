using System.Text.Json;
using Npgsql;
using PhoneShopErp.Modules.Tenancy.Application;
using PhoneShopErp.Persistence;
using PhoneShopErp.Platform.Abstractions;
using PhoneShopErp.Platform.Audit;
using PhoneShopErp.Platform.Errors;
using PhoneShopErp.Platform.Idempotency;
using PhoneShopErp.Platform.Security;
using PhoneShopErp.Platform.Tenancy;

namespace PhoneShopErp.Modules.Tenancy.Infrastructure;

/// <summary>
/// Tenant lifecycle with server-generated identifiers.
///
/// Onboarding is the only flow that starts pre-tenant and later applies tenant
/// context inside the same transaction. The tenant id is generated here; the
/// slug, display name, locale and timezone are the only client inputs, and none
/// of them becomes an authorisation input.
/// </summary>
public sealed class PostgresTenantService : ITenantService
{
    private const string CreateScope = "tenant.create";

    private readonly IPreTenantUnitOfWorkFactory preTenantUnitOfWorkFactory;
    private readonly ITenantUnitOfWorkFactory tenantUnitOfWorkFactory;
    private readonly IOnboardingRequestStore onboardingRequestStore;
    private readonly IAuditStore auditStore;
    private readonly IIdGenerator idGenerator;
    private readonly IClock clock;

    public PostgresTenantService(
        IPreTenantUnitOfWorkFactory preTenantUnitOfWorkFactory,
        ITenantUnitOfWorkFactory tenantUnitOfWorkFactory,
        IOnboardingRequestStore onboardingRequestStore,
        IAuditStore auditStore,
        IIdGenerator idGenerator,
        IClock clock)
    {
        this.preTenantUnitOfWorkFactory = preTenantUnitOfWorkFactory;
        this.tenantUnitOfWorkFactory = tenantUnitOfWorkFactory;
        this.onboardingRequestStore = onboardingRequestStore;
        this.auditStore = auditStore;
        this.idGenerator = idGenerator;
        this.clock = clock;
    }

    public async Task<TenantSummary> CreateAsync(
        TenantOwnerProfile owner,
        CreateTenantRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(request);

        // Onboarding is reachable only by an authenticated provider subject. An
        // empty or oversized subject is rejected here, before any transaction, so
        // an unauthenticated caller cannot create a tenant or even a user row.
        if (string.IsNullOrWhiteSpace(owner.Subject) || owner.Subject.Length > 255)
        {
            throw new PlatformException(
                ErrorCodes.Unauthenticated,
                "The authenticated subject could not be resolved.",
                401);
        }

        RequireIdempotencyKey(idempotencyKey);

        var slug = TenancyValidation.RequireSlug(request.Slug);
        var displayName = TenancyValidation.RequireDisplayName(request.DisplayName, "Display name");
        var locale = TenancyValidation.RequireLocale(request.DefaultLocale);
        var timezone = TenancyValidation.RequireTimezone(request.DefaultTimezone);
        var fingerprint = RequestFingerprint.FromString(
            string.Join('\n', slug, displayName, locale, timezone));

        await using var unitOfWork = await preTenantUnitOfWorkFactory
            .BeginAsync(PreTenantContext.ForSubject(owner.Subject), cancellationToken)
            .ConfigureAwait(false);

        var user = await EnsureUserAsync(unitOfWork, owner, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(user.Status, "active", StringComparison.Ordinal))
        {
            throw new PlatformException(
                ErrorCodes.Forbidden,
                "The user account is not active.",
                403);
        }

        var candidateTenantId = idGenerator.NewId();

        // The pre-tenant idempotency row is keyed by the resolved internal user
        // id, so bind it before reserving. The id comes from the user row, not
        // from the request.
        await unitOfWork
            .ApplyPreTenantUserAsync(user.Id, cancellationToken)
            .ConfigureAwait(false);

        var reservation = await onboardingRequestStore.ReserveAsync(
            unitOfWork,
            idempotencyKey,
            fingerprint,
            candidateTenantId,
            cancellationToken).ConfigureAwait(false);

        if (reservation.IsReplay)
        {
            var replay = await ReadTenantForOwnerAsync(
                unitOfWork,
                reservation.TenantId,
                user.Id,
                cancellationToken).ConfigureAwait(false);

            if (replay is null)
            {
                // The key belongs to a different user. Never disclose the tenant.
                throw new PlatformException(
                    ErrorCodes.IdempotencyKeyReused,
                    "The idempotency key was already used with a different request.",
                    409);
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return replay;
        }

        if (reservation.IsInProgress)
        {
            throw new PlatformException(
                ErrorCodes.IdempotencyInProgress,
                "The onboarding request is being processed. Retry shortly.",
                409);
        }

        await unitOfWork
            .ApplyTenantContextAsync(candidateTenantId, user.Id, cancellationToken)
            .ConfigureAwait(false);

        var now = clock.UtcNow;
        var summary = await InsertTenantAsync(
            unitOfWork,
            candidateTenantId,
            slug,
            displayName,
            locale,
            timezone,
            now,
            cancellationToken).ConfigureAwait(false);

        var membershipId = idGenerator.NewId();
        await InsertMembershipAsync(
            unitOfWork,
            membershipId,
            candidateTenantId,
            user.Id,
            now,
            cancellationToken).ConfigureAwait(false);

        await BootstrapOwnerRoleAsync(
            unitOfWork,
            candidateTenantId,
            membershipId,
            user.Id,
            now,
            cancellationToken).ConfigureAwait(false);

        await auditStore.AppendAsync(
            unitOfWork,
            new AuditEvent(
                idGenerator.NewId(),
                candidateTenantId,
                now,
                AuditActorTypes.User,
                user.Id,
                AuditActions.TenantCreated,
                AuditOutcomes.Success,
                "tenant",
                candidateTenantId,
                null,
                null,
                JsonSerializer.SerializeToDocument(new { slug })),
            cancellationToken).ConfigureAwait(false);

        await onboardingRequestStore.CompleteAsync(
            unitOfWork,
            idempotencyKey,
            candidateTenantId,
            cancellationToken).ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return summary;
    }

    public async Task<TenantSummary> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var context = RequireContext(tenantId);
        PermissionChecker.Demand(context, "tenant.view");

        await using var unitOfWork = await tenantUnitOfWorkFactory
            .BeginAsync(context, cancellationToken)
            .ConfigureAwait(false);

        var summary = await ReadTenantAsync(unitOfWork, tenantId, cancellationToken)
            .ConfigureAwait(false);

        return summary ?? throw new PlatformException(
            ErrorCodes.NotFound,
            "The tenant does not exist or is not visible to this session.",
            404);
    }

    public Task<TenantSummary> SuspendAsync(
        Guid tenantId,
        string reason,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(tenantId, "suspended", AuditActions.TenantSuspended, reason, cancellationToken);

    public Task<TenantSummary> ReactivateAsync(
        Guid tenantId,
        string reason,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(tenantId, "active", AuditActions.TenantReactivated, reason, cancellationToken);

    private async Task<TenantSummary> ChangeStatusAsync(
        Guid tenantId,
        string status,
        string action,
        string reason,
        CancellationToken cancellationToken)
    {
        var context = RequireContext(tenantId);
        PermissionChecker.DemandPlatform(context, "tenant.suspend");
        var validatedReason = TenancyValidation.RequireReason(reason);

        await using var unitOfWork = await tenantUnitOfWorkFactory
            .BeginAsync(context, cancellationToken)
            .ConfigureAwait(false);

        const string sql = """
            UPDATE tenancy.tenant
            SET status = @status
            WHERE id = @id AND tenant_id = platform.current_tenant_id()
            RETURNING id, slug, display_name, status, default_locale, default_timezone,
                      row_version, created_at, updated_at;
            """;

        TenantSummary summary;
        await using (var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction))
        {
            command.Parameters.AddWithValue("status", status);
            command.Parameters.AddWithValue("id", tenantId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new PlatformException(ErrorCodes.NotFound, "The tenant does not exist.", 404);
            }

            summary = MapTenant(reader);
        }

        var now = clock.UtcNow;
        await auditStore.AppendAsync(
            unitOfWork,
            new AuditEvent(
                idGenerator.NewId(),
                tenantId,
                now,
                AuditActorTypes.User,
                context.UserId,
                action,
                AuditOutcomes.Success,
                "tenant",
                tenantId,
                null,
                validatedReason,
                null),
            cancellationToken).ConfigureAwait(false);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return summary;
    }

    private static TenantContext RequireContext(Guid tenantId)
    {
        var context = TenantContextAccessor.Current
            ?? throw new PlatformException(
                ErrorCodes.TenantContextMissing,
                "A tenant context is required for this operation.",
                400);

        if (tenantId == Guid.Empty || context.TenantId != tenantId)
        {
            throw new PlatformException(
                ErrorCodes.TenantMismatch,
                "The requested tenant is not the tenant of the current session.",
                403);
        }

        return context;
    }

    private static void RequireIdempotencyKey(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length is < 8 or > 128)
        {
            throw new PlatformException(
                ErrorCodes.IdempotencyKeyRequired,
                "An Idempotency-Key header of 8-128 characters is required.",
                400);
        }
    }

    private async Task<(Guid Id, string Status)> EnsureUserAsync(
        IUnitOfWork unitOfWork,
        TenantOwnerProfile owner,
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
            command.Parameters.AddWithValue("subject", owner.Subject);
            command.Parameters.AddWithValue("email", (object?)owner.Email ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "display_name",
                string.IsNullOrWhiteSpace(owner.DisplayName) ? owner.Subject : owner.DisplayName);

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
            throw new PlatformException(
                ErrorCodes.Unauthenticated,
                "The authenticated subject could not be resolved.",
                401);
        }

        return (reader.GetGuid(0), reader.GetString(1));
    }

    private static async Task<TenantSummary?> ReadTenantForOwnerAsync(
        IUnitOfWork unitOfWork,
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await unitOfWork.ApplyTenantContextAsync(tenantId, userId, cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT t.id, t.slug, t.display_name, t.status, t.default_locale,
                   t.default_timezone, t.row_version, t.created_at, t.updated_at
            FROM tenancy.tenant t
            JOIN identity.membership m ON m.tenant_id = t.id AND m.user_id = @user_id
            WHERE t.id = @id AND t.tenant_id = platform.current_tenant_id();
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("id", tenantId);
        command.Parameters.AddWithValue("user_id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? MapTenant(reader) : null;
    }

    private static async Task<TenantSummary> ReadTenantAsync(
        IUnitOfWork unitOfWork,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, slug, display_name, status, default_locale, default_timezone,
                   row_version, created_at, updated_at
            FROM tenancy.tenant
            WHERE id = @id AND tenant_id = platform.current_tenant_id();
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("id", tenantId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new PlatformException(ErrorCodes.NotFound, "The tenant does not exist.", 404);
        }

        return MapTenant(reader);
    }

    private static async Task<TenantSummary> InsertTenantAsync(
        IUnitOfWork unitOfWork,
        Guid tenantId,
        string slug,
        string displayName,
        string locale,
        string timezone,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO tenancy.tenant (
                id, tenant_id, slug, display_name, status, default_locale,
                default_timezone, created_at, updated_at, row_version)
            VALUES (
                @id, @tenant_id, @slug, @display_name, 'active', @locale,
                @timezone, @now, @now, 1)
            RETURNING id, slug, display_name, status, default_locale, default_timezone,
                      row_version, created_at, updated_at;
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("id", tenantId);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("slug", slug);
        command.Parameters.AddWithValue("display_name", displayName);
        command.Parameters.AddWithValue("locale", locale);
        command.Parameters.AddWithValue("timezone", timezone);
        command.Parameters.AddWithValue("now", now);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new PlatformException(
                ErrorCodes.InternalError,
                "The tenant could not be created.",
                500);
        }

        return MapTenant(reader);
    }

    private static async Task InsertMembershipAsync(
        IUnitOfWork unitOfWork,
        Guid membershipId,
        Guid tenantId,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO identity.membership (
                id, tenant_id, user_id, status, permission_version,
                created_at, updated_at, row_version)
            VALUES (@id, @tenant_id, @user_id, 'active', 1, @now, @now, 1);
            """;

        await using var command = new NpgsqlCommand(sql, unitOfWork.Connection, unitOfWork.Transaction);
        command.Parameters.AddWithValue("id", membershipId);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("now", now);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Bootstrap grant for the first membership. It is deliberately explicit and
    /// auditable rather than a hidden bypass: a new tenant with no role would be
    /// administratively dead. Every later grant goes through the role API.
    /// </summary>
    private async Task BootstrapOwnerRoleAsync(
        IUnitOfWork unitOfWork,
        Guid tenantId,
        Guid membershipId,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var roleId = idGenerator.NewId();

        const string roleSql = """
            INSERT INTO identity.role (
                id, tenant_id, code, display_name, is_system, is_active,
                created_at, updated_at, row_version)
            VALUES (@id, @tenant_id, 'owner', 'Tenant owner', true, true, @now, @now, 1);
            """;

        await using (var command = new NpgsqlCommand(roleSql, unitOfWork.Connection, unitOfWork.Transaction))
        {
            command.Parameters.AddWithValue("id", roleId);
            command.Parameters.AddWithValue("tenant_id", tenantId);
            command.Parameters.AddWithValue("now", now);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        const string permissionsSql = """
            INSERT INTO identity.role_permission (tenant_id, role_id, permission_code, granted_at)
            SELECT @tenant_id, @role_id, code, @now
            FROM identity.permission
            WHERE scope = 'tenant' AND is_active;
            """;

        await using (var command = new NpgsqlCommand(
            permissionsSql,
            unitOfWork.Connection,
            unitOfWork.Transaction))
        {
            command.Parameters.AddWithValue("tenant_id", tenantId);
            command.Parameters.AddWithValue("role_id", roleId);
            command.Parameters.AddWithValue("now", now);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        const string assignmentSql = """
            INSERT INTO identity.role_assignment (
                id, tenant_id, membership_id, role_id, scope_type, scope_id,
                granted_by_user_id, granted_at, updated_at, row_version)
            VALUES (
                @id, @tenant_id, @membership_id, @role_id, 'tenant', NULL,
                @user_id, @now, @now, 1);
            """;

        await using (var command = new NpgsqlCommand(
            assignmentSql,
            unitOfWork.Connection,
            unitOfWork.Transaction))
        {
            command.Parameters.AddWithValue("id", idGenerator.NewId());
            command.Parameters.AddWithValue("tenant_id", tenantId);
            command.Parameters.AddWithValue("membership_id", membershipId);
            command.Parameters.AddWithValue("role_id", roleId);
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("now", now);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await auditStore.AppendAsync(
            unitOfWork,
            new AuditEvent(
                idGenerator.NewId(),
                tenantId,
                now,
                AuditActorTypes.System,
                userId,
                AuditActions.RoleAssigned,
                AuditOutcomes.Success,
                "role",
                roleId,
                null,
                "Initial tenant owner grant",
                null),
            cancellationToken).ConfigureAwait(false);
    }

    private static TenantSummary MapTenant(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetFieldValue<DateTimeOffset>(8));
}
