using System.Collections.Frozen;

namespace PhoneShopErp.Modules.IdentityAccess.Application;

/// <summary>
/// The only values taken from a validated access token. Tenant, membership and
/// permissions are resolved from the ERP database; a token claim for any of them
/// is ignored (ADR-0013).
/// </summary>
public sealed record AuthenticatedPrincipal(
    string Subject,
    string? ProviderSessionReference,
    string? DisplayName,
    string? Email);

public sealed record TokenValidationResult(
    bool IsValid,
    AuthenticatedPrincipal? Principal,
    string? FailureCode)
{
    public static TokenValidationResult Invalid(string code) => new(false, null, code);

    public static TokenValidationResult Valid(AuthenticatedPrincipal principal) =>
        new(true, principal, null);
}

public interface ISubjectTokenValidator
{
    Task<TokenValidationResult> ValidateAsync(string token, CancellationToken cancellationToken);
}

public sealed record MembershipSummary(
    Guid MembershipId,
    Guid TenantId,
    string TenantSlug,
    string TenantDisplayName,
    string MembershipStatus,
    string TenantStatus);

public sealed record SessionSummary(
    Guid Id,
    Guid TenantId,
    Guid MembershipId,
    int PermissionVersion,
    DateTimeOffset IssuedAt,
    DateTimeOffset IdleExpiresAt,
    DateTimeOffset AbsoluteExpiresAt);

public sealed record SessionRequestMetadata(
    string? DeviceName,
    string? Platform,
    string? UserAgent,
    string? RemoteAddress)
{
    /// <summary>
    /// Only a hash of the binding material is stored. The raw address and user
    /// agent are never persisted, so a database read cannot reconstruct them.
    /// </summary>
    public string BindingFingerprint()
    {
        var material = string.Join(
            '\n',
            DeviceName ?? string.Empty,
            Platform ?? string.Empty,
            UserAgent ?? string.Empty,
            RemoteAddress ?? string.Empty);

        return Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(material)));
    }
}

/// <summary>
/// Everything authorisation needs for one request. Constructed by the session
/// resolver from database rows, never from token claims.
/// </summary>
public sealed record ResolvedSession(
    Guid SessionId,
    Guid TenantId,
    Guid UserId,
    Guid MembershipId,
    int PermissionVersion,
    Guid? DeviceId,
    bool TenantWideScope,
    IReadOnlySet<string> Permissions,
    IReadOnlySet<string> PlatformPermissions,
    IReadOnlySet<Guid> BranchIds,
    IReadOnlySet<Guid> LegalEntityIds,
    IReadOnlySet<Guid> StockLocationIds)
{
    public static ResolvedSession Create(
        Guid sessionId,
        Guid tenantId,
        Guid userId,
        Guid membershipId,
        int permissionVersion,
        Guid? deviceId,
        bool tenantWideScope,
        IEnumerable<string> permissions,
        IEnumerable<string> platformPermissions,
        IEnumerable<Guid> branchIds,
        IEnumerable<Guid> legalEntityIds,
        IEnumerable<Guid> stockLocationIds) =>
        new(
            sessionId,
            tenantId,
            userId,
            membershipId,
            permissionVersion,
            deviceId,
            tenantWideScope,
            permissions.ToFrozenSet(StringComparer.Ordinal),
            platformPermissions.ToFrozenSet(StringComparer.Ordinal),
            branchIds.ToFrozenSet(),
            legalEntityIds.ToFrozenSet(),
            stockLocationIds.ToFrozenSet());
}

public sealed record PermissionSummary(
    string Code,
    string DisplayName,
    string RiskClass,
    string Scope,
    bool IsProtected);

public sealed record RoleSummary(
    Guid Id,
    string Code,
    string DisplayName,
    bool IsSystem,
    int RowVersion,
    IReadOnlyList<string> Permissions);

public sealed record CreateRoleRequest(string Code, string DisplayName, IReadOnlyList<string> Permissions);

public sealed record SetRolePermissionsRequest(IReadOnlyList<string> Permissions);

public sealed record AssignRoleRequest(
    Guid MembershipId,
    Guid RoleId,
    string ScopeType,
    Guid? ScopeId);

public sealed record RoleAssignmentSummary(
    Guid Id,
    Guid MembershipId,
    Guid RoleId,
    string ScopeType,
    Guid? ScopeId,
    DateTimeOffset GrantedAt);

public sealed record DeviceSummary(
    Guid Id,
    Guid UserId,
    string DisplayName,
    string Platform,
    string Status,
    DateTimeOffset RegisteredAt,
    DateTimeOffset LastSeenAt);

public sealed record RevokeDeviceRequest(string Reason);

public sealed record CreateSessionRequest(Guid TenantId, string? DeviceName, string? Platform);
