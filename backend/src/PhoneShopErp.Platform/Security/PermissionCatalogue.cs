using System.Collections.Frozen;
using PhoneShopErp.Platform.Errors;
using PhoneShopErp.Platform.Tenancy;

namespace PhoneShopErp.Platform.Security;

public enum PermissionRiskClass
{
    Low,
    Medium,
    High,
}

/// <summary>
/// A catalogue entry. The database copy in <c>identity.permission</c> is the
/// authoritative data set at runtime; this type is used to validate the seed and
/// to keep risk classification out of handler code.
///
/// <paramref name="Scope"/> mirrors <c>identity.permission.scope</c>: tenant
/// permissions are granted through a tenant role, platform permissions only
/// through <c>identity.platform_grant</c> (ADR-0015). The two sets never mix.
/// </summary>
public sealed record PermissionDefinition(
    string Code,
    string DisplayName,
    PermissionRiskClass RiskClass,
    string Scope,
    bool IsProtected);

/// <summary>
/// Phase 01 platform-foundation permission catalogue. It intentionally contains
/// no catalogue, inventory, purchasing, POS, trade-in, repair or accounting
/// permission. Business permissions arrive with their modules.
/// </summary>
public static class PermissionCatalogue
{
    public const string TenantScope = "tenant";
    public const string PlatformScope = "platform";

    public static readonly FrozenSet<PermissionDefinition> All = new PermissionDefinition[]
    {
        new("audit.view", "View tenant audit events", PermissionRiskClass.Medium, TenantScope, false),
        new("billing.view", "View billing records", PermissionRiskClass.High, TenantScope, true),
        new("branch.configure", "Configure branches", PermissionRiskClass.Medium, TenantScope, false),
        new("branch.create", "Create branches", PermissionRiskClass.Medium, TenantScope, false),
        new("branch.view", "View branches", PermissionRiskClass.Low, TenantScope, false),
        new("device.revoke", "Revoke a registered device", PermissionRiskClass.High, TenantScope, true),
        new("legal_entity.configure", "Configure legal entities", PermissionRiskClass.Medium, TenantScope, false),
        new("legal_entity.view", "View legal entities", PermissionRiskClass.Low, TenantScope, false),
        new("register.configure", "Configure registers", PermissionRiskClass.Medium, TenantScope, false),
        new("register.view", "View registers", PermissionRiskClass.Low, TenantScope, false),
        new("role.assign", "Assign roles to memberships", PermissionRiskClass.High, TenantScope, true),
        new("role.manage", "Create and change roles", PermissionRiskClass.High, TenantScope, true),
        new("role.view", "View roles and permissions", PermissionRiskClass.Low, TenantScope, false),
        new("security_event.view", "Review platform security events", PermissionRiskClass.High, PlatformScope, true),
        new("session.revoke", "Revoke application sessions", PermissionRiskClass.High, TenantScope, true),
        new("session.view", "View own and tenant sessions", PermissionRiskClass.Low, TenantScope, false),
        new("stock_location.configure", "Configure stock locations", PermissionRiskClass.Medium, TenantScope, false),
        new("stock_location.view", "View stock locations", PermissionRiskClass.Low, TenantScope, false),
        new("subscription.manage", "Manage the tenant subscription", PermissionRiskClass.High, TenantScope, true),
        new("subscription.view", "View the tenant subscription", PermissionRiskClass.Low, TenantScope, false),
        new("tenant.configure", "Configure the tenant", PermissionRiskClass.High, TenantScope, true),
        new("tenant.create", "Create a tenant", PermissionRiskClass.High, PlatformScope, true),
        new("tenant.suspend", "Suspend or reactivate a tenant", PermissionRiskClass.High, PlatformScope, true),
        new("tenant.view", "View the tenant", PermissionRiskClass.Low, TenantScope, false),
        new("user.deactivate", "Deactivate a membership", PermissionRiskClass.High, TenantScope, true),
        new("user.invite", "Invite a user to the tenant", PermissionRiskClass.Medium, TenantScope, false),
        new("user.update", "Update a membership", PermissionRiskClass.Medium, TenantScope, false),
        new("user.view", "View tenant memberships", PermissionRiskClass.Low, TenantScope, false),
    }.ToFrozenSet(comparer: null);

    private static readonly FrozenDictionary<string, PermissionDefinition> ByCode =
        All.ToFrozenDictionary(definition => definition.Code, StringComparer.Ordinal);

    public static bool TryGet(string code, out PermissionDefinition definition) =>
        ByCode.TryGetValue(code, out definition!);

    public static bool RequiresHighRiskMfa(string code) =>
        ByCode.TryGetValue(code, out var definition)
        && definition.RiskClass == PermissionRiskClass.High;

    public static bool IsKnown(string code) => ByCode.ContainsKey(code);

    public static bool IsPlatformScope(string code) =>
        ByCode.TryGetValue(code, out var definition)
        && string.Equals(definition.Scope, PlatformScope, StringComparison.Ordinal);
}

/// <summary>
/// Deny-by-default checks. Missing permission, missing context and out-of-scope
/// references all fail closed.
/// </summary>
public static class PermissionChecker
{
    public static bool Has(TenantContext context, string permission)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Permissions.Contains(permission);
    }

    public static void Demand(TenantContext context, string permission)
    {
        if (!Has(context, permission))
        {
            throw new PlatformException(
                ErrorCodes.Forbidden,
                "The operation is not permitted for this session.",
                403);
        }
    }

    public static bool HasPlatform(TenantContext context, string permission)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.PlatformPermissions.Contains(permission);
    }

    /// <summary>
    /// Platform permissions are never granted by a tenant role. A tenant-scoped
    /// session that lacks the platform grant fails closed (ADR-0015).
    /// </summary>
    public static void DemandPlatform(TenantContext context, string permission)
    {
        if (!HasPlatform(context, permission))
        {
            throw new PlatformException(
                ErrorCodes.Forbidden,
                "The operation requires platform administration permission.",
                403);
        }
    }

    /// <summary>
    /// Branch scope is separate from the tenant boundary. A tenant-wide grant
    /// allows every branch in the tenant; otherwise only explicitly assigned
    /// branches are permitted.
    /// </summary>
    public static void DemandBranch(TenantContext context, Guid branchId)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TenantWideScope)
        {
            return;
        }

        if (branchId == Guid.Empty || !context.BranchIds.Contains(branchId))
        {
            throw new PlatformException(
                ErrorCodes.Forbidden,
                "The operation is not permitted for this branch.",
                403);
        }
    }

    public static void DemandLegalEntity(TenantContext context, Guid legalEntityId)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TenantWideScope)
        {
            return;
        }

        if (legalEntityId == Guid.Empty || !context.LegalEntityIds.Contains(legalEntityId))
        {
            throw new PlatformException(
                ErrorCodes.Forbidden,
                "The operation is not permitted for this legal entity.",
                403);
        }
    }

    public static void DemandStockLocation(TenantContext context, Guid stockLocationId)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TenantWideScope)
        {
            return;
        }

        if (stockLocationId == Guid.Empty || !context.StockLocationIds.Contains(stockLocationId))
        {
            throw new PlatformException(
                ErrorCodes.Forbidden,
                "The operation is not permitted for this stock location.",
                403);
        }
    }
}
