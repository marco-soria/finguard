namespace FinGuard.Compliance.Contracts.Events;

/// <summary>
/// Published once per evaluation, in the same transaction that stores it (ADR-0009). Compliance evaluates each
/// accepted submission once, against the limit policy in force at its cut-off date (D-193).
/// </summary>
/// <param name="PolicyVersion">The limit policy version the evaluation used.</param>
/// <param name="Classes">
/// Exactly four entries, one per <see cref="AssetClassCode"/> in enum order. Amounts are book value in USD (ADR-0010).
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
    IReadOnlyList<ClassEvaluation> Classes)
{
    // Identifiers only: never dates, amounts or statuses in logs (D-162).
    public override string ToString() =>
        $"{nameof(EvaluationCompleted)} {{ EventId = {EventId}, EvaluationId = {EvaluationId}, SubmissionId = {SubmissionId} }}";
}
