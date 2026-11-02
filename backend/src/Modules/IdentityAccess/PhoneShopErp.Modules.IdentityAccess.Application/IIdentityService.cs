namespace PhoneShopErp.Modules.IdentityAccess.Application;

/// <summary>
/// Resolves the ERP application session from the validated token. Returns
/// <see langword="null"/> for an unknown, revoked or expired session; callers must
/// treat that as unauthenticated, never as anonymous access.
/// </summary>
public interface ISessionResolver
{
    Task<ResolvedSession?> ResolveAsync(
        AuthenticatedPrincipal principal,
        CancellationToken cancellationToken);
}

public interface IIdentityService : ISessionResolver
{
    /// <summary>
    /// Just-in-time provisioning from a trusted provider subject. Existing users
    /// keep their ERP identifier; the provider subject never becomes an
    /// authorisation input beyond identifying the row.
    /// </summary>
    Task<Guid> EnsureUserAsync(
        AuthenticatedPrincipal principal,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MembershipSummary>> GetMyMembershipsAsync(
        AuthenticatedPrincipal principal,
        CancellationToken cancellationToken);

    Task<SessionSummary> CreateSessionAsync(
        AuthenticatedPrincipal principal,
        CreateSessionRequest request,
        SessionRequestMetadata metadata,
        CancellationToken cancellationToken);

    Task RevokeSessionAsync(
        Guid sessionId,
        string reason,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionSummary>> GetMySessionsAsync(
        AuthenticatedPrincipal principal,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DeviceSummary>> GetMyDevicesAsync(CancellationToken cancellationToken);

    Task RevokeDeviceAsync(Guid deviceId, string reason, CancellationToken cancellationToken);

    Task<IReadOnlyList<PermissionSummary>> GetPermissionsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleSummary>> GetRolesAsync(CancellationToken cancellationToken);

    Task<RoleSummary> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken);

    Task<RoleSummary> SetRolePermissionsAsync(
        Guid roleId,
        SetRolePermissionsRequest request,
        CancellationToken cancellationToken);

    Task<RoleAssignmentSummary> AssignRoleAsync(
        AssignRoleRequest request,
        CancellationToken cancellationToken);

    Task RevokeRoleAssignmentAsync(
        Guid assignmentId,
        string reason,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(CancellationToken cancellationToken);
}
