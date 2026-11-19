using FinGuard.Portfolio.Features.ReceiveSubmission.Csv;

namespace FinGuard.Portfolio.Features.ReceiveSubmission;

/// <summary>201 for both outcomes (D-112): the client reads <see cref="Status"/>. At most 100 errors.</summary>
public sealed record SubmissionReceivedResponse(
    Guid SubmissionId, string Status, DateTimeOffset ReceivedAt, int RowCount, int ErrorCount,
    IReadOnlyList<CsvError> Errors)
{
    // Identifiers and counts only (D-77): Wolverine may log a reply with ToString().
    public override string ToString() =>
        $"{nameof(SubmissionReceivedResponse)} {{ SubmissionId = {SubmissionId}, Status = {Status}, RowCount = {RowCount}, ErrorCount = {ErrorCount} }}";
}
