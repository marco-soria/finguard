namespace FinGuard.Compliance.Features.GetEvaluation;

/// <summary>
/// One stored evaluation. Amounts are book value in USD; ratios are exact, unrounded decimals (D-197). Clients format
/// them for display and never recompute a status (R-39).
/// </summary>
/// <param name="AlertThreshold">The policy snapshot's alert threshold, so clients can format ratios (D-206).</param>
/// <param name="Classes">One entry per asset class, in the order Cash, FixedIncome, Shares, RealEstate.</param>
/// <param name="Alerts">The classes with status Warning or Breached, in the same order.</param>
/// <param name="Proposal">The rebalance proposal (ADR-0013); null for evaluations stored before it existed (D-238).</param>
public sealed record EvaluationResponse(
    Guid EvaluationId, Guid SubmissionId, DateOnly CutOffDate, string OverallStatus, int PolicyVersion,
    DateTimeOffset EvaluatedAt, decimal TotalBookValue, decimal AlertThreshold,
    IReadOnlyList<ClassEvaluationItem> Classes,
    IReadOnlyList<AlertItem> Alerts,
    ProposalItem? Proposal);

public sealed record ClassEvaluationItem(
    string AssetClass, decimal BookValue, decimal Share, decimal Limit, decimal Utilization, string Status);

public sealed record AlertItem(string AssetClass, string Status, decimal Utilization);

/// <param name="Status"><c>NotNeeded</c>, <c>Full</c> or <c>Partial</c>.</param>
/// <param name="Moves">The classes that change, in presentation order (D-235); empty for <c>NotNeeded</c>.</param>
public sealed record ProposalItem(string Status, IReadOnlyList<MoveItem> Moves);

/// <param name="Change">Proposed minus current book value: negative reduces the class, positive adds to it.</param>
public sealed record MoveItem(string AssetClass, decimal CurrentBookValue, decimal ProposedBookValue, decimal Change);
