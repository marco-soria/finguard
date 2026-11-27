namespace FinGuard.Portfolio.Features.ListSubmissions;

/// <summary>One page, newest first. <c>NextCursor</c> is null on the last page (D-299; added to the D-100 envelope).</summary>
public sealed record SubmissionListResponse(IReadOnlyList<SubmissionListItem> Items, string? NextCursor);

public sealed record SubmissionListItem(
    Guid SubmissionId, DateOnly CutOffDate, string FileName, int SizeBytes, string Status, DateTimeOffset ReceivedAt,
    int RowCount, int ErrorCount, bool IsCurrent);
