using FinGuard.Compliance.Features.StorePortfolioSnapshot;
using FinGuard.Portfolio.Contracts;

namespace FinGuard.UnitTests.Compliance;

public sealed class SubmissionAcceptedHandlerTests
{
    public static TheoryData<string, AssetClassTotal[]> InvalidTotals => new()
    {
        { "three entries", [Total(AssetClassCode.Cash, 1m), Total(AssetClassCode.FixedIncome, 1m), Total(AssetClassCode.Shares, 1m)] },
        {
            "five entries",
            [
                Total(AssetClassCode.Cash, 1m), Total(AssetClassCode.FixedIncome, 1m), Total(AssetClassCode.Shares, 1m),
                Total(AssetClassCode.RealEstate, 1m), Total(AssetClassCode.Cash, 1m),
            ]
        },
        {
            "class repeated",
            [
                Total(AssetClassCode.Cash, 1m), Total(AssetClassCode.Cash, 1m), Total(AssetClassCode.Shares, 1m),
                Total(AssetClassCode.RealEstate, 1m),
            ]
        },
        {
            "unknown class",
            [
                Total(AssetClassCode.Cash, 1m), Total(AssetClassCode.FixedIncome, 1m), Total(AssetClassCode.Shares, 1m),
                Total((AssetClassCode)99, 1m),
            ]
        },
        { "negative total", Valid(shares: -0.01m) },
        { "zero sum", Valid(cash: 0m) },
    };

    [Theory]
    [MemberData(nameof(InvalidTotals))]
    public void HasValidTotals_InvalidTotals_ReturnsFalse(string scenario, AssetClassTotal[] totals)
    {
        Assert.False(SubmissionAcceptedHandler.HasValidTotals(totals), scenario);
    }

    [Fact]
    public void HasValidTotals_FourClassesWithZerosAndPositiveSum_ReturnsTrue()
    {
        Assert.True(SubmissionAcceptedHandler.HasValidTotals(Valid()));
    }

    // Cash only by default: zeros elsewhere are valid.
    private static AssetClassTotal[] Valid(decimal cash = 100m, decimal shares = 0m) =>
    [
        Total(AssetClassCode.Cash, cash), Total(AssetClassCode.FixedIncome, 0m), Total(AssetClassCode.Shares, shares),
        Total(AssetClassCode.RealEstate, 0m),
    ];

    private static AssetClassTotal Total(AssetClassCode assetClass, decimal bookValue) => new(assetClass, bookValue);
}
