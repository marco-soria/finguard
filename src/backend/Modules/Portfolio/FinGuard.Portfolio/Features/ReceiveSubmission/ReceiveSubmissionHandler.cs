using FinGuard.BuildingBlocks.Results;
using FinGuard.Portfolio.Features.ReceiveSubmission.Csv;
using FinGuard.Portfolio.Persistence;
using Microsoft.Extensions.Logging;

namespace FinGuard.Portfolio.Features.ReceiveSubmission;

/// <summary>
/// Command handler (ADR-0007): writes through the module DbContext. No SaveChanges call: Wolverine's
/// AutoApplyTransactions middleware saves and commits after Handle returns, so the submission, its bytes and its
/// positions or errors commit together or not at all (D-111).
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

        var id = Guid.CreateVersion7(receivedAt);
        var fileName = FileNameSanitizer.Sanitize(command.FileName);
        var sha256 = Sha256Hex.Compute(command.Content);
        var csv = PortfolioCsvReader.Read(command.Content);

        var submission = csv.IsValid
            ? Submission.Accept(id, command.CutOffDate, fileName, command.Content.Length, sha256, receivedAt, csv.RowCount)
            : Submission.Reject(
                id, command.CutOffDate, fileName, command.Content.Length, sha256, receivedAt, csv.RowCount, csv.ErrorCount);
        db.Add(submission);
        db.Add(SubmissionContent.For(id, command.Content));
        db.AddRange(csv.Positions.Select(p => Position.Create(id, p.RowNumber, p.PositionId, p.AssetClass, p.MarketValueUsd)));
        db.AddRange(csv.Errors.Select((e, i) => SubmissionError.Create(id, i, e.Row, e.Column, e.Code, e.Message)));

        LogStored(logger, id, submission.SizeBytes, submission.Status, submission.RowCount, submission.ErrorCount);
        return new SubmissionReceivedResponse(
            id, submission.Status.ToString(), receivedAt, submission.RowCount, submission.ErrorCount, csv.Errors);
    }

    // Transport checks (D-112): first failure wins and nothing is stored. Content checks live in PortfolioCsvReader.
    private static Error? Validate(ReceiveSubmission command, DateOnly todayUtc) => command switch
    {
        { Content.Length: 0 } => ReceiveSubmissionErrors.FileEmpty,
        { Content.Length: > ReceiveSubmissionErrors.MaxFileBytes } => ReceiveSubmissionErrors.FileTooLarge,
        _ when !Path.GetExtension(command.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            => ReceiveSubmissionErrors.NotCsv,
        _ when command.CutOffDate > todayUtc => ReceiveSubmissionErrors.CutOffDateInFuture,
        _ => null,
    };

    // Id, size, status and counts only: never the file name, content, hash, amounts or cut-off date (D-91).
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Stored submission {SubmissionId} ({SizeBytes} bytes): {Status}, {RowCount} rows, {ErrorCount} errors")]
    private static partial void LogStored(
        ILogger logger, Guid submissionId, int sizeBytes, SubmissionStatus status, int rowCount, int errorCount);
}
