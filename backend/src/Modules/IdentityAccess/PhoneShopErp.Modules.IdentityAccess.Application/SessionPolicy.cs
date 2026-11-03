using PhoneShopErp.Platform.Errors;

namespace PhoneShopErp.Modules.IdentityAccess.Application;

/// <summary>
/// Session lifetime and MFA classes. These are security configuration values, not
/// business policy: they are bounded, validated, and can be changed without a
/// schema migration. The access token itself is owned by the identity provider.
/// </summary>
public sealed record SessionPolicy
{
    /// <summary>
    /// Back-office session bounds approved by D-002 (30 minute idle, 12 hour
    /// absolute). POS/mobile lifetimes are bound to device and shift and are set
    /// by D-012/D-013, so they must not reuse this value.
    /// </summary>
    public static SessionPolicy Default { get; } = new(
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(12));

    public SessionPolicy(TimeSpan idleTimeout, TimeSpan absoluteTimeout)
    {
        if (idleTimeout < TimeSpan.FromMinutes(5) || idleTimeout > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(
                nameof(idleTimeout),
                "Idle timeout must be between 5 minutes and 24 hours.");
        }

        if (absoluteTimeout < idleTimeout || absoluteTimeout > TimeSpan.FromDays(30))
        {
            throw new ArgumentOutOfRangeException(
                nameof(absoluteTimeout),
                "Absolute timeout must be at least the idle timeout and at most 30 days.");
        }

        IdleTimeout = idleTimeout;
        AbsoluteTimeout = absoluteTimeout;
    }

    public TimeSpan IdleTimeout { get; }

    public TimeSpan AbsoluteTimeout { get; }
}

/// <summary>
/// Deny-by-default authorisation decisions that do not need a database. Role
/// names are never used; only permission codes drive behaviour.
/// </summary>
public static class AuthorizationRules
{
    public static bool HasTenantPermission(ResolvedSession session, string permission)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Permissions.Contains(permission);
    }

    public static bool HasPlatformPermission(ResolvedSession session, string permission)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.PlatformPermissions.Contains(permission);
    }

    public static void DemandPlatform(ResolvedSession session, string permission)
    {
        if (!HasPlatformPermission(session, permission))
        {
            throw new PlatformException(
                ErrorCodes.Forbidden,
                "The operation requires platform administration permission.",
                403);
        }
    }

    public static void ValidateScope(string scopeType, Guid? scopeId)
    {
        if (scopeType is not ("tenant" or "legal_entity" or "branch" or "stock_location"))
        {
            throw new PlatformException(
                ErrorCodes.ValidationFailed,
                "Scope type must be one of: tenant, legal_entity, branch, stock_location.",
                400);
        }

        if (scopeType == "tenant" && scopeId is not null)
        {
            throw new PlatformException(
                ErrorCodes.ValidationFailed,
                "A tenant-wide role assignment must not include a scope identifier.",
                400);
        }

        if (scopeType != "tenant" && (scopeId is null || scopeId == Guid.Empty))
        {
            throw new PlatformException(
                ErrorCodes.ValidationFailed,
                "A scoped role assignment requires a scope identifier.",
                400);
        }
    }
}
