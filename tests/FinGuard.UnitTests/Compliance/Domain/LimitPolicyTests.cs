using FinGuard.BuildingBlocks.Results;
using FinGuard.Compliance.Domain;
using static FinGuard.UnitTests.Compliance.Domain.PolicyTestData;

namespace FinGuard.UnitTests.Compliance.Domain;

public sealed class LimitPolicyTests
{
    [Fact]
    public void Create_SeedValues_Succeeds()
    {
        // 0.85 x (0.40 + 0.30 + 0.25 + 0.25) = 0.85 x 1.20 = 1.02 >= 1.
        var result = LimitPolicy.Create(1, SeedEffectiveFrom, SeedLimits(), new Ratio(0.90m), new Ratio(0.85m));

        Assert.True(result.IsSuccess);
        Assert.Equal(SeedLimits(), result.Value.Limits);
        Assert.Equal((1, SeedEffectiveFrom), (result.Value.Version, result.Value.EffectiveFrom));
    }

    [Fact]
    public void Create_TargetTimesSumExactlyOne_Succeeds()
    {
        // 0.80 x 1.25 = 1.
        var result = Create(Limits(0.40m, 0.30m, 0.30m, 0.25m), alert: 0.90m, target: 0.80m);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_LimitAndThresholdExactlyOne_Succeeds()
    {
        var result = Create(Limits(1m, 0.30m, 0.25m, 0.25m), alert: 1m, target: 0.85m);

        Assert.True(result.IsSuccess);
    }

    public static TheoryData<string, decimal[], decimal, decimal> Failures() => new()
    {
        { "Compliance.Policy.LimitOutOfRange", [0m, 0.30m, 0.25m, 0.25m], 0.90m, 0.85m },
        { "Compliance.Policy.LimitOutOfRange", [1.0001m, 0.30m, 0.25m, 0.25m], 0.90m, 0.85m },
        { "Compliance.Policy.AlertThresholdOutOfRange", [0.40m, 0.30m, 0.25m, 0.25m], 0m, 0.85m },
        { "Compliance.Policy.AlertThresholdOutOfRange", [0.40m, 0.30m, 0.25m, 0.25m], 1.0001m, 0.85m },
        { "Compliance.Policy.TargetRatioOutOfRange", [0.40m, 0.30m, 0.25m, 0.25m], 0.90m, 0m },
        { "Compliance.Policy.TargetRatioOutOfRange", [0.40m, 0.30m, 0.25m, 0.25m], 1m, 1m },
        { "Compliance.Policy.TargetNotBelowAlert", [0.40m, 0.30m, 0.25m, 0.25m], 0.90m, 0.90m },
        // 0.85 x 1.17 = 0.9945 < 1.
        { "Compliance.Policy.LimitsTooTightForTarget", [0.40m, 0.27m, 0.25m, 0.25m], 0.90m, 0.85m },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void Create_InvalidConfiguration_ReturnsValidationError(
        string code, decimal[] limits, decimal alert, decimal target)
    {
        var result = Create(Limits(limits[0], limits[1], limits[2], limits[3]), alert, target);

        Assert.True(result.IsFailure);
        Assert.Equal((code, ErrorType.Validation), (result.Error.Code, result.Error.Type));
    }

    [Fact]
    public void Create_ClassWithoutLimit_ReturnsLimitMissing()
    {
        var limits = SeedLimits();
        limits.Remove(AssetClass.Shares);

        var result = Create(limits, alert: 0.90m, target: 0.85m);

        Assert.Equal(LimitPolicyErrors.LimitMissing, result.Error);
    }

    [Fact]
    public void ToString_AnyPolicy_PrintsVersionOnly()
    {
        Assert.Equal("LimitPolicy { Version = 1 }", Seed().ToString());
    }

    private static Result<LimitPolicy> Create(Dictionary<AssetClass, Ratio> limits, decimal alert, decimal target) =>
        LimitPolicy.Create(2, new DateOnly(2026, 1, 1), limits, new Ratio(alert), new Ratio(target));

    private static Dictionary<AssetClass, Ratio> Limits(decimal cash, decimal fixedIncome, decimal shares, decimal realEstate) =>
        new()
        {
            [AssetClass.Cash] = new Ratio(cash),
            [AssetClass.FixedIncome] = new Ratio(fixedIncome),
            [AssetClass.Shares] = new Ratio(shares),
            [AssetClass.RealEstate] = new Ratio(realEstate),
        };
}
