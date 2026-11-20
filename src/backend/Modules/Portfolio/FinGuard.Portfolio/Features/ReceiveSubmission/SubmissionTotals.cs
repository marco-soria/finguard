using FinGuard.Portfolio.Contracts;
using FinGuard.Portfolio.Features.ReceiveSubmission.Csv;
using FinGuard.Portfolio.Persistence;

namespace FinGuard.Portfolio.Features.ReceiveSubmission;

/// <summary>Builds the <c>SubmissionAccepted</c> totals (D-153): one entry per asset class, zeros included.</summary>
internal static class SubmissionTotals
{
    public static IReadOnlyList<AssetClassTotal> From(IEnumerable<ParsedPosition> positions)
    {
        var sums = positions.GroupBy(p => p.AssetClass).ToDictionary(g => g.Key, g => g.Sum(p => p.BookValue));
        return Enum.GetValues<AssetClass>()
            .Select(c => new AssetClassTotal(c.ToContract(), sums.GetValueOrDefault(c)))
            .OrderBy(t => t.AssetClass)
            .ToList();
    }

    // The only mapping from the Portfolio type to the published one (D-154).
    public static AssetClassCode ToContract(this AssetClass assetClass) => assetClass switch
    {
        AssetClass.Cash => AssetClassCode.Cash,
        AssetClass.FixedIncome => AssetClassCode.FixedIncome,
        AssetClass.Shares => AssetClassCode.Shares,
        AssetClass.RealEstate => AssetClassCode.RealEstate,
        _ => throw new ArgumentOutOfRangeException(nameof(assetClass), assetClass, null),
    };
}
