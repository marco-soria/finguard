using FinGuard.Portfolio.Features.ReceiveSubmission.Csv;

namespace FinGuard.UnitTests.Portfolio;

// samples/portfolios/README.md states these outcomes (D-118).
public sealed class SamplePortfoliosTests
{
    [Theory]
    [InlineData("valid-balanced.csv", 9, 0)]
    [InlineData("valid-shares-heavy.csv", 5, 0)]
    [InlineData("invalid-mixed.csv", 9, 8)]
    public void Read_SampleFile_MatchesReadme(string file, int rowCount, int errorCount)
    {
        var result = PortfolioCsvReader.Read(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", file)));

        Assert.Equal((rowCount, errorCount), (result.RowCount, result.ErrorCount));
    }

    [Fact]
    public void Read_InvalidMixed_HasOneErrorPerRowInReadmeOrder()
    {
        var result = PortfolioCsvReader.Read(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "invalid-mixed.csv")));

        Assert.Equal(
            [
                (3, "PositionIdRequired"), (4, "AssetClassUnknown"), (5, "BookValueNegative"),
                (6, "BookValueTooPrecise"), (7, "BookValueInvalid"), (8, "BookValueRequired"),
                (9, "CurrencyNotSupported"), (10, "PositionIdDuplicated"),
            ],
            result.Errors.Select(e => (e.Row!.Value, e.Code["Portfolio.Csv.".Length..])));
    }
}
