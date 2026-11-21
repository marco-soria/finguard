namespace FinGuard.Compliance.Features.GetEvaluation;

/// <summary>
/// One stored evaluation. Amounts are book value in USD; ratios are exact, unrounded decimals (D-197). Clients format
/// them for display and never recompute a status (R-39).
/// </summary>
/// <param name="Classes">One entry per asset class, in the order Cash, FixedIncome, Shares, RealEstate.</param>
/// <param name="Alerts">The classes with status Warning or Breached, in the same order.</param>
public sealed record EvaluationResponse(
    Guid EvaluationId, Guid SubmissionId, DateOnly CutOffDate, string OverallStatus, int PolicyVersion,
    DateTimeOffset EvaluatedAt, decimal TotalBookValue, IReadOnlyList<ClassEvaluationItem> Classes,
    IReadOnlyList<AlertItem> Alerts);

public sealed record ClassEvaluationItem(
    string AssetClass, decimal BookValue, decimal Share, decimal Limit, decimal Utilization, string Status);

public sealed record AlertItem(string AssetClass, string Status, decimal Utilization);
