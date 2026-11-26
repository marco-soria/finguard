namespace FinGuard.Compliance.Contracts.Events;

/// <summary>
/// Published once per evaluation, in the same transaction that stores it (ADR-0009). Compliance evaluates each
/// accepted submission once, against the limit policy in force at its cut-off date (D-193).
/// </summary>
/// <param name="PolicyVersion">The limit policy version the evaluation used.</param>
/// <param name="ProposalStatus">Outcome of the rebalance proposal; the amounts are in <c>Classes</c> (ADR-0013).</param>
/// <param name="Classes">
/// Exactly four entries, one per <see cref="AssetClassCode"/> in enum order. Amounts are book value in USD (ADR-0010).
/// </param>
/// <param name="AlertThreshold">
/// Utilization at or above which a class is in alert, from the policy snapshot the evaluation used (for example 0.90).
/// </param>
/// <param name="RebalanceTargetRatio">
/// Share of its limit the proposal brings alert classes down to, from the policy snapshot the evaluation used (for
/// example 0.85, ADR-0013).
/// </param>
/// <remarks>Additive changes only; a breaking change means a new event type (ADR-0011 rule).</remarks>
public sealed record EvaluationCompleted(
    Guid EventId,
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid EvaluationId,
    Guid SubmissionId,
    DateOnly CutOffDate,
    ComplianceStatusCode OverallStatus,
    int PolicyVersion,
    IReadOnlyList<ClassEvaluation> Classes,
    ProposalStatusCode ProposalStatus,
    decimal AlertThreshold,
    decimal RebalanceTargetRatio)
{
    // Identifiers only: never dates, amounts or statuses in logs (D-162).
    public override string ToString() =>
        $"{nameof(EvaluationCompleted)} {{ EventId = {EventId}, EvaluationId = {EvaluationId}, SubmissionId = {SubmissionId} }}";
}
