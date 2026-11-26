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

    // Shared by the generated checks of Brief #17 (engine) and Brief #18 (advisory). A total from 1.00 to 10^12 USD (uniform number of digits), split over the classes with random weights. Half the
    // cases use free weights (zero in about one case out of five, so zero-value classes are common); the other half
    // use weights near each class's limit, which yields Compliant, Warning and middle classes.
    public static Dictionary<AssetClass, Money> RandomPortfolio(Random random)
    {
        var digits = random.Next(3, 15);
        var low = 1L;
        for (var d = 1; d < digits; d++)
        {
            low *= 10;
        }

        var totalCents = random.NextInt64(low, (low * 10) + 1);
        int[] nearLimits = [400, 300, 250, 250];
        var weights = random.Next(2) == 0
            ? Enumerable.Range(0, 4).Select(_ => random.Next(5) == 0 ? 0 : random.Next(1, 1001)).ToArray()
            : nearLimits.Select(w => w * random.Next(70, 101) / 100).ToArray();
        if (weights.Sum() == 0)
        {
            weights[random.Next(4)] = 1;
        }

        var cents = new long[4];
        var weightTotal = weights.Sum();
        for (var k = 0; k < 3; k++)
        {
            cents[k] = (long)((decimal)totalCents * weights[k] / weightTotal);
        }

        cents[3] = totalCents - cents[0] - cents[1] - cents[2];
        return Values(cents[0] / 100m, cents[1] / 100m, cents[2] / 100m, cents[3] / 100m);
    }
}
