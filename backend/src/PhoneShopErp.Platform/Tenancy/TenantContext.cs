using System.Collections.Frozen;

namespace PhoneShopErp.Platform.Tenancy;

/// <summary>
/// Immutable, scoped tenant context. It exists only after a validated token has
/// been resolved to a server-side application session and an active membership.
/// Values are frozen at construction; a caller cannot widen scope by mutating a
/// collection it passed in.
/// </summary>
public sealed class TenantContext
{
    private readonly FrozenSet<string> permissions;
    private readonly FrozenSet<string> platformPermissions;
    private readonly FrozenSet<Guid> branchIds;
    private readonly FrozenSet<Guid> legalEntityIds;
    private readonly FrozenSet<Guid> stockLocationIds;

    private TenantContext(
        Guid tenantId,
        Guid userId,
        Guid sessionId,
        Guid membershipId,
        int permissionVersion,
        bool tenantWideScope,
        IEnumerable<string> permissions,
        IEnumerable<string> platformPermissions,
        IEnumerable<Guid> branchIds,
        IEnumerable<Guid> legalEntityIds,
        IEnumerable<Guid> stockLocationIds)
    {
        TenantId = tenantId;
        UserId = userId;
        SessionId = sessionId;
        MembershipId = membershipId;
        PermissionVersion = permissionVersion;
        TenantWideScope = tenantWideScope;
        this.permissions = permissions.ToFrozenSet(StringComparer.Ordinal);
        this.platformPermissions = platformPermissions.ToFrozenSet(StringComparer.Ordinal);
        this.branchIds = branchIds.ToFrozenSet();
        this.legalEntityIds = legalEntityIds.ToFrozenSet();
        this.stockLocationIds = stockLocationIds.ToFrozenSet();
    }

    public Guid TenantId { get; }

    public Guid UserId { get; }

    public Guid SessionId { get; }

    public Guid MembershipId { get; }

    public int PermissionVersion { get; }

    /// <summary>
    /// True when the current session holds a tenant-wide grant. Branch/entity
    /// scope is otherwise deny-by-default and must be checked per referenced
    /// object.
    /// </summary>
    public bool TenantWideScope { get; }

    public IReadOnlySet<string> Permissions => permissions;

    /// <summary>
    /// Platform-administration permissions held by the caller through
    /// <c>identity.platform_grant</c>. They are deliberately separate from tenant
    /// permissions so a tenant role can never grant platform scope (ADR-0015).
    /// </summary>
    public IReadOnlySet<string> PlatformPermissions => platformPermissions;

    /// <summary>Branches the current session may act on. Empty means no branch scope was granted.</summary>
    public IReadOnlySet<Guid> BranchIds => branchIds;

    public IReadOnlySet<Guid> LegalEntityIds => legalEntityIds;

    public IReadOnlySet<Guid> StockLocationIds => stockLocationIds;

    public static TenantContext Create(
        Guid tenantId,
        Guid userId,
        Guid sessionId,
        Guid membershipId,
        int permissionVersion,
        bool tenantWideScope = false,
        IEnumerable<string>? permissions = null,
        IEnumerable<string>? platformPermissions = null,
        IEnumerable<Guid>? branchIds = null,
        IEnumerable<Guid>? legalEntityIds = null,
        IEnumerable<Guid>? stockLocationIds = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant identifier must not be empty.", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session identifier must not be empty.", nameof(sessionId));
        }

        if (membershipId == Guid.Empty)
        {
            throw new ArgumentException("Membership identifier must not be empty.", nameof(membershipId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(permissionVersion, 1);

        return new TenantContext(
            tenantId,
            userId,
            sessionId,
            membershipId,
            permissionVersion,
            tenantWideScope,
            permissions ?? [],
            platformPermissions ?? [],
            branchIds ?? [],
            legalEntityIds ?? [],
            stockLocationIds ?? []);
    }
}
