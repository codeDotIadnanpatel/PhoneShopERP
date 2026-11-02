using System.Text.Json;

namespace PhoneShopErp.Platform.Audit;

public static class AuditActions
{
    public const string TenantCreated = "tenant.created";
    public const string TenantSuspended = "tenant.suspended";
    public const string TenantReactivated = "tenant.reactivated";
    public const string TenantConfigured = "tenant.configured";
    public const string MembershipCreated = "membership.created";
    public const string MembershipUpdated = "membership.updated";
    public const string SessionCreated = "session.created";
    public const string SessionRevoked = "session.revoked";
    public const string SessionTenantSwitched = "session.tenant_switched";
    public const string DeviceRegistered = "device.registered";
    public const string DeviceRevoked = "device.revoked";
    public const string RoleCreated = "role.created";
    public const string RolePermissionsChanged = "role.permissions_changed";
    public const string RoleAssigned = "role.assigned";
    public const string RoleAssignmentRevoked = "role_assignment.revoked";
    public const string LegalEntityCreated = "legal_entity.created";
    public const string BranchCreated = "branch.created";
    public const string StockLocationCreated = "stock_location.created";
    public const string RegisterCreated = "register.created";
    public const string SubscriptionPlanChanged = "subscription.plan_changed";
    public const string AccessDenied = "access.denied";
    public const string TenantContextRejected = "tenant_context.rejected";
}

public static class AuditActorTypes
{
    public const string User = "user";
    public const string System = "system";
    public const string PlatformAdmin = "platform_admin";
}

public static class AuditOutcomes
{
    public const string Success = "success";
    public const string Denied = "denied";
    public const string Failure = "failure";
}

/// <summary>
/// A tenant-scoped audit record. <see cref="Detail"/> is already redacted by the
/// caller; the store does not attempt to guess what is sensitive.
/// </summary>
public sealed record AuditEvent(
    Guid Id,
    Guid TenantId,
    DateTimeOffset OccurredAt,
    string ActorType,
    Guid? ActorUserId,
    string Action,
    string Outcome,
    string? TargetType,
    Guid? TargetId,
    string? CorrelationId,
    string? Reason,
    JsonDocument? Detail);

/// <summary>
/// A pre-tenant or platform security event. It deliberately has no tenant
/// business payload: an unknown account, a provider outage or a rejected token
/// must not leak tenant data into a platform-only table.
/// </summary>
public sealed record SecurityEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Category,
    string Severity,
    string Outcome,
    string? ActorReference,
    string? CorrelationId,
    JsonDocument? Detail);

public static class SecurityEventCategories
{
    public const string Authentication = "authentication";
    public const string Authorization = "authorization";
    public const string TenantContext = "tenant_context";
    public const string IdentityProvider = "identity_provider";
    public const string Migration = "migration";
    public const string RateLimit = "rate_limit";
}

public static class SecurityEventSeverities
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string High = "high";
    public const string Critical = "critical";
}
