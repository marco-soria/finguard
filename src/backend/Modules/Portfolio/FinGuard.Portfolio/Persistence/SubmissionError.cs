using FinGuard.BuildingBlocks.Tenancy;

namespace FinGuard.Portfolio.Persistence;

/// <summary>One stored validation error of a rejected submission (D-115, D-116). Never holds a cell value.</summary>
internal sealed class SubmissionError : ITenantOwned
{
    public const int ColumnMaxLength = 32;
    public const int CodeMaxLength = 64;
    public const int MessageMaxLength = 200;

    private SubmissionError()
    {
    }

    public Guid SubmissionId { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>0-based position in the reported order (row, then canonical column).</summary>
    public int Ordinal { get; private set; }

    public int? RowNumber { get; private set; }

    public string? Column { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public static SubmissionError Create(
        Guid submissionId, int ordinal, int? rowNumber, string? column, string code, string message) =>
        new()
        {
            SubmissionId = submissionId,
            Ordinal = ordinal,
            RowNumber = rowNumber,
            Column = column,
            Code = code,
            Message = message,
        };
}
