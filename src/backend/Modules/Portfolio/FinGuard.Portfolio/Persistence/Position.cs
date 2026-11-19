using FinGuard.BuildingBlocks.Tenancy;

namespace FinGuard.Portfolio.Persistence;

/// <summary>One CSV row of an accepted submission (D-116). The description is never stored (D-114).</summary>
internal sealed class Position : ITenantOwned
{
    public const int PositionIdMaxLength = 64;

    private Position()
    {
    }

    public Guid SubmissionId { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Spreadsheet row of the CSV line (header = 1).</summary>
    public int RowNumber { get; private set; }

    public string PositionId { get; private set; } = string.Empty;

    public AssetClass AssetClass { get; private set; }

    public decimal MarketValueUsd { get; private set; }

    public static Position Create(
        Guid submissionId, int rowNumber, string positionId, AssetClass assetClass, decimal marketValueUsd) =>
        new()
        {
            SubmissionId = submissionId,
            RowNumber = rowNumber,
            PositionId = positionId,
            AssetClass = assetClass,
            MarketValueUsd = marketValueUsd,
        };
}
