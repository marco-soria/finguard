namespace FinGuard.Portfolio.Features.ListSubmissions;

/// <summary>Envelope so paging can arrive later without a breaking change (D-100).</summary>
public sealed record SubmissionListResponse(IReadOnlyList<SubmissionListItem> Items);

public sealed record SubmissionListItem(
    Guid SubmissionId, DateOnly CutOffDate, string FileName, int SizeBytes, string Status, DateTimeOffset ReceivedAt,
    int RowCount, int ErrorCount);
