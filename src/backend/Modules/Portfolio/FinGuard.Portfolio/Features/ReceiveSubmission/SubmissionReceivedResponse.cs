namespace FinGuard.Portfolio.Features.ReceiveSubmission;

public sealed record SubmissionReceivedResponse(Guid SubmissionId, string Status, DateTimeOffset ReceivedAt);
