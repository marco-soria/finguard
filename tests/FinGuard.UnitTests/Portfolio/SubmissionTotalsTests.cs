using FinGuard.Portfolio.Contracts;
using FinGuard.Portfolio.Features.ReceiveSubmission;
using FinGuard.Portfolio.Features.ReceiveSubmission.Csv;
using FinGuard.Portfolio.Persistence;

namespace FinGuard.UnitTests.Portfolio;

public sealed class SubmissionTotalsTests
{
    [Fact]
    public void From_OneClassOnly_OtherThreeAreZero()
    {
        var totals = SubmissionTotals.From([Position(AssetClass.Shares, 125.50m)]);

        Assert.Equal(
            [
                new AssetClassTotal(AssetClassCode.Cash, 0m),
                new AssetClassTotal(AssetClassCode.FixedIncome, 0m),
                new AssetClassTotal(AssetClassCode.Shares, 125.50m),
                new AssetClassTotal(AssetClassCode.RealEstate, 0m),
            ],
            totals);
    }

    [Fact]
    public void From_SeveralPositionsPerClass_SumsExactly()
    {
        var totals = SubmissionTotals.From(
            [Position(AssetClass.Cash, 0.10m), Position(AssetClass.RealEstate, 7m), Position(AssetClass.Cash, 0.20m)]);

        Assert.Equal(0.30m, totals.Single(t => t.AssetClass == AssetClassCode.Cash).BookValue);
        Assert.Equal(7m, totals.Single(t => t.AssetClass == AssetClassCode.RealEstate).BookValue);
    }

    [Fact]
    public void From_PositionsInAnyOrder_TotalsFollowAssetClassCodeOrder()
    {
        var totals = SubmissionTotals.From([Position(AssetClass.RealEstate, 1m), Position(AssetClass.Cash, 2m)]);

        Assert.Equal(Enum.GetValues<AssetClassCode>(), totals.Select(t => t.AssetClass));
    }

    [Fact]
    public void ToContract_EveryAssetClass_MapsToItsOwnCode()
    {
        var mapped = Enum.GetValues<AssetClass>().Select(c => c.ToContract());

        Assert.Equal(
            [AssetClassCode.Cash, AssetClassCode.FixedIncome, AssetClassCode.Shares, AssetClassCode.RealEstate], mapped);
        Assert.Equal(Enum.GetValues<AssetClassCode>().Length, Enum.GetValues<AssetClass>().Length);
    }

    private static ParsedPosition Position(AssetClass assetClass, decimal bookValue) =>
        new(2, "P-1", assetClass, bookValue);
}
