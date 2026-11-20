using FinGuard.Compliance.Domain;

namespace FinGuard.UnitTests.Compliance.Domain;

/// <summary>The seed policy (AGENTS.md) and helpers to build evaluation inputs.</summary>
internal static class PolicyTestData
{
    public const decimal Total = 1_000_000.00m;

    public static readonly DateOnly SeedEffectiveFrom = new(2000, 1, 1);

    public static readonly DateTimeOffset EvaluatedAt = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    public static Dictionary<AssetClass, Ratio> SeedLimits() => new()
    {
        [AssetClass.Cash] = new Ratio(0.40m),
        [AssetClass.FixedIncome] = new Ratio(0.30m),
        [AssetClass.Shares] = new Ratio(0.25m),
        [AssetClass.RealEstate] = new Ratio(0.25m),
    };

    public static LimitPolicy Seed() =>
        LimitPolicy.Create(1, SeedEffectiveFrom, SeedLimits(), new Ratio(0.90m), new Ratio(0.85m)).Value;

    public static Dictionary<AssetClass, Money> Values(decimal cash, decimal fixedIncome, decimal shares, decimal realEstate) =>
        new()
        {
            [AssetClass.Cash] = new Money(cash),
            [AssetClass.FixedIncome] = new Money(fixedIncome),
            [AssetClass.Shares] = new Money(shares),
            [AssetClass.RealEstate] = new Money(realEstate),
        };

    public static Evaluation Evaluate(IReadOnlyDictionary<AssetClass, Money> values) =>
        Evaluation.Create(Guid.NewGuid(), new DateOnly(2026, 6, 30), values, Seed(), EvaluatedAt);
}
