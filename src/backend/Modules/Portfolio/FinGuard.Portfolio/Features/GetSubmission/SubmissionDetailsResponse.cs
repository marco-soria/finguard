namespace FinGuard.Portfolio.Features.GetSubmission;

public sealed record SubmissionDetailsResponse(
    Guid SubmissionId, DateOnly CutOffDate, string FileName, int SizeBytes, string Status, DateTimeOffset ReceivedAt,
    int RowCount, int ErrorCount, bool IsCurrent, IReadOnlyList<SubmissionErrorItem> Errors);

/// <summary>A stored validation error; <see cref="Row"/> and <see cref="Column"/> are null for file errors.</summary>
public sealed record SubmissionErrorItem(int? Row, string? Column, string Code, string Message);
