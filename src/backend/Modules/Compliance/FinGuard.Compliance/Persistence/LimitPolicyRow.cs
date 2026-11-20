using FinGuard.Compliance.Domain;

namespace FinGuard.Compliance.Persistence;

/// <summary>
/// One immutable version of the global limit policy in <c>compliance.limit_policies</c> (D-175). Not tenant-owned:
/// limits are the same for every entity. Rows are never updated; a change is a new version.
/// </summary>
internal sealed class LimitPolicyRow
{
    public int Version { get; init; }

    public DateOnly EffectiveFrom { get; init; }

    public decimal Cash { get; init; }

    public decimal FixedIncome { get; init; }

    public decimal Shares { get; init; }

    public decimal RealEstate { get; init; }

    public decimal AlertThreshold { get; init; }

    public decimal RebalanceTargetRatio { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Maps through <see cref="LimitPolicy.Create"/>. The CHECK constraints should make a failure impossible, so a stored
    /// row that breaks an invariant is corrupted data and throws (D-177).
    /// </summary>
    public LimitPolicy ToDomain()
    {
        var limits = new Dictionary<AssetClass, Ratio>
        {
            [AssetClass.Cash] = new(Cash),
            [AssetClass.FixedIncome] = new(FixedIncome),
            [AssetClass.Shares] = new(Shares),
            [AssetClass.RealEstate] = new(RealEstate),
        };

        var result = LimitPolicy.Create(
            Version, EffectiveFrom, limits, new Ratio(AlertThreshold), new Ratio(RebalanceTargetRatio));

        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"Limit policy version {Version} is invalid: {result.Error.Code}.");
    }
}
