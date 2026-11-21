using FinGuard.Compliance.Persistence;
using FinGuard.Portfolio.Contracts;
using FinGuard.Portfolio.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;
using EvaluatePortfolioCommand = FinGuard.Compliance.Features.EvaluatePortfolio.EvaluatePortfolio;

namespace FinGuard.Compliance.Features.StorePortfolioSnapshot;

/// <summary>
/// Stores Compliance's own snapshot of an accepted submission (D-157), idempotent by <c>EventId</c> (ADR-0009).
/// Writes through <see cref="ComplianceDbContext"/> only; Wolverine's EF middleware saves and commits. The envelope
/// tenant is already checked against the payload tenant by <c>TenantPropagationMiddleware</c>.
/// A new snapshot cascades <see cref="EvaluatePortfolioCommand"/>, which evaluates in its own transaction: the received fact
/// commits even when the evaluation fails (D-192). A duplicate event cascades nothing.
/// </summary>
public sealed partial class SubmissionAcceptedHandler(TimeProvider time, ILogger<SubmissionAcceptedHandler> logger)
{
    public async Task<OutgoingMessages> Handle(SubmissionAccepted @event, ComplianceDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(db);

        if (await db.Set<PortfolioSnapshot>().AnyAsync(s => s.SourceEventId == @event.EventId, ct))
        {
            return [];
        }

        // Portfolio guarantees these totals, so this should never fire; after retries the event goes to the error queue.
        if (!HasValidTotals(@event.Totals))
        {
            LogInvalidTotals(logger, @event.SubmissionId);
            throw new InvalidOperationException("SubmissionAccepted carries invalid asset class totals.");
        }

        var totals = @event.Totals.ToDictionary(t => t.AssetClass, t => t.BookValue);
        db.Add(PortfolioSnapshot.Create(
            @event.SubmissionId, @event.CutOffDate, @event.ReceivedAt,
            totals[AssetClassCode.Cash], totals[AssetClassCode.FixedIncome], totals[AssetClassCode.Shares],
            totals[AssetClassCode.RealEstate], @event.EventId, time.GetUtcNow()));
        LogStored(logger, @event.SubmissionId);
        return [new EvaluatePortfolioCommand(@event.SubmissionId)];
    }

    /// <summary>One entry per asset class, each at least 0, and a total above 0 (D-153, AGENTS.md validation).</summary>
    internal static bool HasValidTotals(IReadOnlyList<AssetClassTotal>? totals) =>
        totals is not null
        && totals.Select(t => t.AssetClass).Where(Enum.IsDefined).Distinct().Count() == Enum.GetValues<AssetClassCode>().Length
        && totals.Count == Enum.GetValues<AssetClassCode>().Length
        && totals.All(t => t.BookValue >= 0)
        && totals.Sum(t => t.BookValue) > 0;

    // Submission id only: never amounts or dates in logs.
    [LoggerMessage(Level = LogLevel.Information, Message = "Stored portfolio snapshot for submission {SubmissionId}")]
    private static partial void LogStored(ILogger logger, Guid submissionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Invalid asset class totals for submission {SubmissionId}")]
    private static partial void LogInvalidTotals(ILogger logger, Guid submissionId);
}
