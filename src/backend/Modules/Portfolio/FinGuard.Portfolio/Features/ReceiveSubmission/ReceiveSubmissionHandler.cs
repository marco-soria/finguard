using FinGuard.BuildingBlocks.Results;
using FinGuard.Portfolio.Persistence;
using Microsoft.Extensions.Logging;

namespace FinGuard.Portfolio.Features.ReceiveSubmission;

/// <summary>
/// Command handler (ADR-0007): writes through the module DbContext. No SaveChanges call: Wolverine's
/// AutoApplyTransactions middleware saves and commits after Handle returns, so both rows commit or neither does.
/// </summary>
public sealed partial class ReceiveSubmissionHandler(
    PortfolioDbContext db, TimeProvider time, ILogger<ReceiveSubmissionHandler> logger)
{
    public Result<SubmissionReceivedResponse> Handle(ReceiveSubmission command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var receivedAt = time.GetUtcNow();
        if (Validate(command, DateOnly.FromDateTime(receivedAt.UtcDateTime)) is { } error)
        {
            return error;
        }

        var submission = Submission.Receive(
            Guid.CreateVersion7(receivedAt),
            command.CutOffDate,
            FileNameSanitizer.Sanitize(command.FileName),
            command.Content.Length,
            Sha256Hex.Compute(command.Content),
            receivedAt);
        db.Add(submission);
        db.Add(SubmissionContent.For(submission.Id, command.Content));

        LogReceived(logger, submission.Id, submission.SizeBytes);
        return new SubmissionReceivedResponse(submission.Id, submission.Status.ToString(), submission.ReceivedAt);
    }

    // First failure wins (brief #8). Content checks (UTF-8, rows, columns) are Phase 2.
    private static Error? Validate(ReceiveSubmission command, DateOnly todayUtc) => command switch
    {
        { Content.Length: 0 } => ReceiveSubmissionErrors.FileEmpty,
        { Content.Length: > ReceiveSubmissionErrors.MaxFileBytes } => ReceiveSubmissionErrors.FileTooLarge,
        _ when !Path.GetExtension(command.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            => ReceiveSubmissionErrors.NotCsv,
        _ when command.CutOffDate > todayUtc => ReceiveSubmissionErrors.CutOffDateInFuture,
        _ => null,
    };

    // Id and size only: never the file name, content, hash or cut-off date (D-91).
    [LoggerMessage(Level = LogLevel.Information, Message = "Received submission {SubmissionId} ({SizeBytes} bytes)")]
    private static partial void LogReceived(ILogger logger, Guid submissionId, int sizeBytes);
}
