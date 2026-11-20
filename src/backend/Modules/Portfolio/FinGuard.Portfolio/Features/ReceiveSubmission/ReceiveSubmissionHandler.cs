using FinGuard.BuildingBlocks.Results;
using FinGuard.Portfolio.Features.ReceiveSubmission.Csv;
using FinGuard.Portfolio.Persistence;
using Microsoft.EntityFrameworkCore;
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
    public async Task<Result<SubmissionReceivedResponse>> Handle(ReceiveSubmission command, CancellationToken ct)
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

        // Idempotency (D-11, D-139): the earliest submission with the same bytes and cut-off date, whatever its status.
        // The tenant filter applies. Nothing is written for a duplicate; a simultaneous twin may still be stored (D-140).
        if (await FindDuplicateAsync(command.CutOffDate, sha256, ct) is { } duplicate)
        {
            return duplicate;
        }

        var csv = PortfolioCsvReader.Read(command.Content);

        var submission = csv.IsValid
            ? Submission.Accept(id, command.CutOffDate, fileName, command.Content.Length, sha256, receivedAt, csv.RowCount)
            : Submission.Reject(
                id, command.CutOffDate, fileName, command.Content.Length, sha256, receivedAt, csv.RowCount, csv.ErrorCount);
        db.Add(submission);
        db.Add(SubmissionContent.For(id, command.Content));
        db.AddRange(csv.Positions.Select(p => Position.Create(id, p.RowNumber, p.PositionId, p.AssetClass, p.BookValue)));
        db.AddRange(csv.Errors.Select((e, i) => SubmissionError.Create(id, i, e.Row, e.Column, e.Code, e.Message)));

        LogStored(logger, id, submission.SizeBytes, submission.Status, submission.RowCount, submission.ErrorCount);
        return new SubmissionReceivedResponse(
            id, submission.Status.ToString(), receivedAt, submission.RowCount, submission.ErrorCount, csv.Errors,
            Duplicate: false);
    }

    private async Task<SubmissionReceivedResponse?> FindDuplicateAsync(DateOnly cutOffDate, string sha256, CancellationToken ct)
    {
        var existing = await db.Set<Submission>().AsNoTracking()
            .Where(s => s.CutOffDate == cutOffDate && s.Sha256 == sha256)
            .OrderBy(s => s.ReceivedAt).ThenBy(s => s.Id)
            .FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            return null;
        }

        var errors = await db.Set<SubmissionError>().AsNoTracking()
            .Where(e => e.SubmissionId == existing.Id)
            .OrderBy(e => e.Ordinal)
            .Take(PortfolioCsvReader.MaxErrors)
            .Select(e => new CsvError(e.RowNumber, e.Column, e.Code, e.Message))
            .ToListAsync(ct);

        LogDuplicate(logger, existing.Id);
        return new SubmissionReceivedResponse(
            existing.Id, existing.Status.ToString(), existing.ReceivedAt, existing.RowCount, existing.ErrorCount, errors,
            Duplicate: true);
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

    // Id only: never the file name, hash or cut-off date (D-91).
    [LoggerMessage(Level = LogLevel.Information, Message = "Duplicate upload of submission {SubmissionId}")]
    private static partial void LogDuplicate(ILogger logger, Guid submissionId);
}
