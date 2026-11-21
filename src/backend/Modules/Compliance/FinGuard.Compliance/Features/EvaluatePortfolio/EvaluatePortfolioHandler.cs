using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Compliance.Domain;
using FinGuard.Compliance.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace FinGuard.Compliance.Features.EvaluatePortfolio;

/// <summary>
/// Evaluates a stored snapshot against the policy in force at its cut-off date, stores the evaluation and cascades
/// <c>EvaluationCompleted</c> in the same transaction (ADR-0009). Idempotent: one evaluation per submission (D-193).
/// It commits apart from the snapshot, so a failure here can be replayed from the error queue (D-192). Missing data is
/// a bug, not a business outcome, so it throws with identifiers only (D-191).
/// </summary>
public sealed partial class EvaluatePortfolioHandler(TimeProvider time, ILogger<EvaluatePortfolioHandler> logger)
{
    public async Task<OutgoingMessages> Handle(
        EvaluatePortfolio command, ComplianceDbContext db, IEvaluationRepository evaluations,
        ILimitPolicyRepository policies, ITenantContext tenant, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(evaluations);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(tenant);

        var outgoing = new OutgoingMessages();
        if (await evaluations.ExistsForSubmissionAsync(command.SubmissionId, ct))
        {
            return outgoing;
        }

        var snapshot = await db.Set<PortfolioSnapshot>().AsNoTracking()
                           .SingleOrDefaultAsync(s => s.SubmissionId == command.SubmissionId, ct)
                       ?? throw new InvalidOperationException(
                           $"No portfolio snapshot exists for submission {command.SubmissionId}.");

        // Portfolio rejects cut-off dates before the first policy (D-190), so null means old data or a bug.
        var policy = await policies.GetEffectiveAsync(snapshot.CutOffDate, ct)
                     ?? throw new InvalidOperationException(
                         $"No limit policy is in force for submission {command.SubmissionId}.");

        var classValues = new Dictionary<AssetClass, Money>
        {
            [AssetClass.Cash] = new(snapshot.Cash),
            [AssetClass.FixedIncome] = new(snapshot.FixedIncome),
            [AssetClass.Shares] = new(snapshot.Shares),
            [AssetClass.RealEstate] = new(snapshot.RealEstate),
        };
        var evaluation = Evaluation.Create(snapshot.SubmissionId, snapshot.CutOffDate, classValues, policy, time.GetUtcNow());

        evaluations.Add(evaluation);
        outgoing.Add(ContractMapping.ToEvent(evaluation, tenant.RequiredTenantId));
        LogEvaluated(logger, evaluation.SubmissionId, evaluation.Id);
        return outgoing;
    }

    // Identifiers only: never statuses, amounts or dates (D-162).
    [LoggerMessage(Level = LogLevel.Information, Message = "Evaluated submission {SubmissionId} as {EvaluationId}")]
    private static partial void LogEvaluated(ILogger logger, Guid submissionId, Guid evaluationId);
}
