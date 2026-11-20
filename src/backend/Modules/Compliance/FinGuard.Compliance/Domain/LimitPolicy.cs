using FinGuard.BuildingBlocks.Results;

namespace FinGuard.Compliance.Domain;

/// <summary>
/// One immutable version of the global limit policy (D-175): maximum share of the total book value per class.
/// An invalid configuration is an expected failure, so <see cref="Create"/> returns a <see cref="Result{T}"/> (D-170).
/// </summary>
public sealed class LimitPolicy
{
    private LimitPolicy(
        int version, DateOnly effectiveFrom, IReadOnlyDictionary<AssetClass, Ratio> limits, Ratio alertThreshold,
        Ratio rebalanceTargetRatio)
    {
        Version = version;
        EffectiveFrom = effectiveFrom;
        Limits = limits;
        AlertThreshold = alertThreshold;
        RebalanceTargetRatio = rebalanceTargetRatio;
    }

    public int Version { get; }

    public DateOnly EffectiveFrom { get; }

    /// <summary>Exactly one limit per <see cref="AssetClass"/>, each in (0, 1].</summary>
    public IReadOnlyDictionary<AssetClass, Ratio> Limits { get; }

    public Ratio AlertThreshold { get; }

    /// <summary>Kept here for its invariant; the rebalance engine that uses it is Phase 4.</summary>
    public Ratio RebalanceTargetRatio { get; }

    public static Result<LimitPolicy> Create(
        int version, DateOnly effectiveFrom, IReadOnlyDictionary<AssetClass, Ratio> limits, Ratio alertThreshold,
        Ratio rebalanceTargetRatio)
    {
        ArgumentNullException.ThrowIfNull(limits);

        var classes = Enum.GetValues<AssetClass>();
        if (classes.Any(c => !limits.ContainsKey(c)))
        {
            return LimitPolicyErrors.LimitMissing;
        }

        var ownLimits = classes.ToDictionary(c => c, c => limits[c]);
        if (ownLimits.Values.Any(l => l.Value <= 0m || l.Value > 1m))
        {
            return LimitPolicyErrors.LimitOutOfRange;
        }

        if (alertThreshold.Value <= 0m || alertThreshold.Value > 1m)
        {
            return LimitPolicyErrors.AlertThresholdOutOfRange;
        }

        if (rebalanceTargetRatio.Value <= 0m || rebalanceTargetRatio.Value >= 1m)
        {
            return LimitPolicyErrors.TargetRatioOutOfRange;
        }

        if (rebalanceTargetRatio.Value >= alertThreshold.Value)
        {
            return LimitPolicyErrors.TargetNotBelowAlert;
        }

        // Rebalancing every class to its target must still be able to hold the whole portfolio.
        if (rebalanceTargetRatio.Value * ownLimits.Values.Sum(l => l.Value) < 1m)
        {
            return LimitPolicyErrors.LimitsTooTightForTarget;
        }

        return new LimitPolicy(version, effectiveFrom, ownLimits, alertThreshold, rebalanceTargetRatio);
    }

    public PolicySnapshot ToSnapshot() => new(Version, Limits, AlertThreshold, RebalanceTargetRatio);

    // D-162: a policy is identified by its version.
    public override string ToString() => $"{nameof(LimitPolicy)} {{ Version = {Version} }}";
}
