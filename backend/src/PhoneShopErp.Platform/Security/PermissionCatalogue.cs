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
/// </summary>
public sealed record PermissionDefinition(
    string Code,
    string DisplayName,
    PermissionRiskClass RiskClass,
    bool IsProtected);

/// <summary>
/// Phase 01 platform-foundation permission catalogue. It intentionally contains
/// no catalogue, inventory, purchasing, POS, trade-in, repair or accounting
/// permission. Business permissions arrive with their modules.
/// </summary>
public static class PermissionCatalogue
{
    public static readonly FrozenSet<PermissionDefinition> All = new PermissionDefinition[]
    {
        new("audit.view", "View tenant audit events", PermissionRiskClass.Medium, false),
        new("billing.view", "View billing records", PermissionRiskClass.High, true),
        new("branch.configure", "Configure branches", PermissionRiskClass.Medium, false),
        new("branch.create", "Create branches", PermissionRiskClass.Medium, false),
        new("branch.view", "View branches", PermissionRiskClass.Low, false),
        new("device.revoke", "Revoke a registered device", PermissionRiskClass.High, true),
        new("legal_entity.configure", "Configure legal entities", PermissionRiskClass.Medium, false),
        new("legal_entity.view", "View legal entities", PermissionRiskClass.Low, false),
        new("register.configure", "Configure registers", PermissionRiskClass.Medium, false),
        new("register.view", "View registers", PermissionRiskClass.Low, false),
        new("role.assign", "Assign roles to memberships", PermissionRiskClass.High, true),
        new("role.manage", "Create and change roles", PermissionRiskClass.High, true),
        new("role.view", "View roles and permissions", PermissionRiskClass.Low, false),
        new("security_event.view", "View platform security events", PermissionRiskClass.High, true),
        new("session.revoke", "Revoke application sessions", PermissionRiskClass.High, true),
        new("session.view", "View own and tenant sessions", PermissionRiskClass.Low, false),
        new("stock_location.configure", "Configure stock locations", PermissionRiskClass.Medium, false),
        new("stock_location.view", "View stock locations", PermissionRiskClass.Low, false),
        new("subscription.manage", "Manage the tenant subscription", PermissionRiskClass.High, true),
        new("subscription.view", "View the tenant subscription", PermissionRiskClass.Low, false),
        new("tenant.configure", "Configure the tenant", PermissionRiskClass.High, true),
        new("tenant.suspend", "Suspend or reactivate a tenant", PermissionRiskClass.High, true),
        new("tenant.view", "View the tenant", PermissionRiskClass.Low, false),
        new("user.deactivate", "Deactivate a membership", PermissionRiskClass.High, true),
        new("user.invite", "Invite a user to the tenant", PermissionRiskClass.Medium, false),
        new("user.update", "Update a membership", PermissionRiskClass.Medium, false),
        new("user.view", "View tenant memberships", PermissionRiskClass.Low, false),
    }.ToFrozenSet(comparer: null);

    private static readonly FrozenDictionary<string, PermissionDefinition> ByCode =
        All.ToFrozenDictionary(definition => definition.Code, StringComparer.Ordinal);

    public static bool TryGet(string code, out PermissionDefinition definition) =>
        ByCode.TryGetValue(code, out definition!);

    public static bool RequiresHighRiskMfa(string code) =>
        ByCode.TryGetValue(code, out var definition)
        && definition.RiskClass == PermissionRiskClass.High;

    public static bool IsKnown(string code) => ByCode.ContainsKey(code);
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
