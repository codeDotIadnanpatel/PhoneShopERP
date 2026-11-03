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

/// <summary>
/// Phase 01 identity slice: provisioning, the caller's own memberships, and the
/// session lifecycle.
///
/// Device registration and role administration (create/assign/revoke) are
/// deliberately absent: their write paths are not implemented in Phase 01, and an
/// interface member that throws <c>NOT_IMPLEMENTED</c> would imply a contract the
/// platform does not honour. The initial owner grant is the only role
/// administration Phase 01 performs, inside onboarding.
/// </summary>
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

    /// <summary>
    /// The caller's own memberships, readable before a tenant is selected. Tenant
    /// slug and display name are included so the client can render a tenant
    /// picker; no other tenant data is exposed by this path.
    /// </summary>
    Task<IReadOnlyList<MembershipSummary>> GetMyMembershipsAsync(
        AuthenticatedPrincipal principal,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates the application session for one tenant. Switching tenant revokes
    /// the current session and creates a new one; there is no in-place tenant
    /// mutation (ADR-0013).
    /// </summary>
    Task<SessionSummary> CreateSessionAsync(
        AuthenticatedPrincipal principal,
        CreateSessionRequest request,
        SessionRequestMetadata metadata,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revokes the caller's own session, or any session in the current tenant when
    /// the session holds <c>session.revoke</c>. Requires a resolved tenant context.
    /// </summary>
    Task RevokeSessionAsync(
        Guid sessionId,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// The caller's own sessions in the current tenant. Requires a resolved tenant
    /// context; the pre-tenant read policy exposes a session only by its provider
    /// reference, which is not an enumeration key.
    /// </summary>
    Task<IReadOnlyList<SessionSummary>> GetMySessionsAsync(CancellationToken cancellationToken);
}
