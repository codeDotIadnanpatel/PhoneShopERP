namespace PhoneShopErp.Modules.SaasPlatform.Application;

public enum SubscriptionState
{
    Trial,
    Active,
    PastDue,
    Grace,
    Restricted,
    Suspended,
    Cancelled,
}

public sealed record PlanSummary(
    string Code,
    string DisplayName,
    bool IsActive,
    IReadOnlyDictionary<string, PlanFeature> Features);

public sealed record PlanFeature(string Code, bool IsEnabled, long? LimitValue);

public sealed record SubscriptionSummary(
    Guid Id,
    string PlanCode,
    SubscriptionState State,
    DateTimeOffset? CurrentPeriodEnd,
    DateTimeOffset? CancelledAt,
    int RowVersion);

public sealed record EntitlementSnapshot(
    SubscriptionSummary? Subscription,
    IReadOnlyDictionary<string, PlanFeature> Features);

public sealed record EntitlementDecision(bool IsAllowed, string? ReasonCode, string? Message);

/// <summary>
/// Entitlement evaluation. Values come from data; nothing is hard-coded in a
/// handler. D-014 has not supplied plan values, grace periods or transition
/// rules, so an absent or unknown feature fails closed.
/// </summary>
public static class EntitlementPolicy
{
    public const string FeatureNotConfigured = "entitlement.feature_not_configured";
    public const string FeatureDisabled = "entitlement.feature_disabled";
    public const string SubscriptionSuspended = "entitlement.subscription_suspended";
    public const string SubscriptionCancelled = "entitlement.subscription_cancelled";
    public const string SubscriptionMissing = "entitlement.subscription_missing";

    /// <summary>
    /// Recovery/export is deliberately independent of billing state. A tenant in
    /// restricted or suspended mode must still be able to retrieve its data.
    /// </summary>
    public static bool AllowsDataExport() => true;

    public static EntitlementDecision Evaluate(EntitlementSnapshot? snapshot, string featureCode)
    {
        if (snapshot?.Subscription is null)
        {
            return Denied(SubscriptionMissing, "No subscription is configured for this tenant.");
        }

        if (snapshot.Subscription.State is SubscriptionState.Cancelled)
        {
            return Denied(SubscriptionCancelled, "The subscription is cancelled.");
        }

        if (snapshot.Subscription.State is SubscriptionState.Suspended)
        {
            return Denied(SubscriptionSuspended, "The subscription is suspended.");
        }

        if (!snapshot.Features.TryGetValue(featureCode, out var feature))
        {
            return Denied(
                FeatureNotConfigured,
                "The feature is not configured for this plan. Entitlement values are pending D-014.");
        }

        if (!feature.IsEnabled)
        {
            return Denied(FeatureDisabled, "The feature is not enabled for this plan.");
        }

        return new EntitlementDecision(true, null, null);
    }

    /// <summary>
    /// Read-only mode never deletes data and never blocks export. Handlers that
    /// mutate data ask this question; read handlers do not.
    /// </summary>
    public static bool IsReadOnly(SubscriptionState? state) =>
        state is SubscriptionState.Restricted or SubscriptionState.Grace or SubscriptionState.PastDue;

    private static EntitlementDecision Denied(string code, string message) =>
        new(false, code, message);
}

public interface IEntitlementService
{
    Task<EntitlementSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<SubscriptionSummary?> GetCurrentSubscriptionAsync(CancellationToken cancellationToken);

    Task<SubscriptionSummary> ChangePlanAsync(
        Guid subscriptionId,
        string planCode,
        int expectedRowVersion,
        string reason,
        CancellationToken cancellationToken);
}

public sealed record ChangePlanRequest(string PlanCode, int RowVersion, string Reason);
