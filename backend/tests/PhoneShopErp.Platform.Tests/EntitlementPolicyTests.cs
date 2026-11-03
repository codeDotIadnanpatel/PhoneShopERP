using PhoneShopErp.Modules.SaasPlatform.Application;
using Xunit;

namespace PhoneShopErp.Platform.Tests;

public sealed class EntitlementPolicyTests
{
    private static EntitlementSnapshot Snapshot(
        SubscriptionState state,
        params PlanFeature[] features) =>
        new(
            new SubscriptionSummary(
                Guid.NewGuid(),
                "test-plan",
                state,
                null,
                null,
                1),
            features.ToDictionary(feature => feature.Code, StringComparer.Ordinal));

    [Fact]
    public void Evaluate_FailsClosedWithoutASubscription()
    {
        var decision = EntitlementPolicy.Evaluate(null, "pos.sale");

        Assert.False(decision.IsAllowed);
        Assert.Equal(EntitlementPolicy.SubscriptionMissing, decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_DeniesCancelledAndSuspendedRegardlessOfFeature()
    {
        var cancelled = EntitlementPolicy.Evaluate(
            Snapshot(SubscriptionState.Cancelled, new PlanFeature("pos.sale", true, null)),
            "pos.sale");
        var suspended = EntitlementPolicy.Evaluate(
            Snapshot(SubscriptionState.Suspended, new PlanFeature("pos.sale", true, null)),
            "pos.sale");

        Assert.Equal(EntitlementPolicy.SubscriptionCancelled, cancelled.ReasonCode);
        Assert.Equal(EntitlementPolicy.SubscriptionSuspended, suspended.ReasonCode);
    }

    [Fact]
    public void Evaluate_DeniesAnUnknownFeatureBecausePlanValuesArePending()
    {
        var decision = EntitlementPolicy.Evaluate(
            Snapshot(SubscriptionState.Active),
            "pos.sale");

        Assert.False(decision.IsAllowed);
        Assert.Equal(EntitlementPolicy.FeatureNotConfigured, decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_DeniesAConfiguredButDisabledFeature()
    {
        var decision = EntitlementPolicy.Evaluate(
            Snapshot(SubscriptionState.Active, new PlanFeature("pos.sale", false, null)),
            "pos.sale");

        Assert.False(decision.IsAllowed);
        Assert.Equal(EntitlementPolicy.FeatureDisabled, decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_AllowsAnEnabledFeature()
    {
        var decision = EntitlementPolicy.Evaluate(
            Snapshot(SubscriptionState.Active, new PlanFeature("pos.sale", true, 5)),
            "pos.sale");

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.ReasonCode);
    }

    [Theory]
    [InlineData(SubscriptionState.PastDue)]
    [InlineData(SubscriptionState.Grace)]
    [InlineData(SubscriptionState.Restricted)]
    public void IsReadOnly_BillingDegradationNeverDeletesData(SubscriptionState state)
    {
        Assert.True(EntitlementPolicy.IsReadOnly(state));
    }

    [Theory]
    [InlineData(SubscriptionState.Trial)]
    [InlineData(SubscriptionState.Active)]
    public void IsReadOnly_IsFalseForWritableStates(SubscriptionState state)
    {
        Assert.False(EntitlementPolicy.IsReadOnly(state));
    }

    [Fact]
    public void DataExport_IsIndependentOfBillingState()
    {
        Assert.True(EntitlementPolicy.AllowsDataExport());
    }
}
