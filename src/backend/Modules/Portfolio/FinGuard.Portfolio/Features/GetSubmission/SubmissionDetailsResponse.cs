namespace FinGuard.Portfolio.Features.GetSubmission;

public sealed record SubmissionDetailsResponse(
    Guid SubmissionId, DateOnly CutOffDate, string FileName, int SizeBytes, string Status, DateTimeOffset ReceivedAt);
