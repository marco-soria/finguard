namespace FinGuard.Compliance.Contracts;

// Outside .Events: every type in an Events namespace is an integration event (ADR-0009, D-163).

/// <summary>The evaluation of one asset class in an <c>EvaluationCompleted</c> event.</summary>
/// <param name="BookValue">Book value in USD (ADR-0010).</param>
/// <param name="Limit">Maximum share of the total from the policy version used, for example 0.25.</param>
/// <param name="Utilization">(book value / total) / limit, unrounded. Exactly 1 is still within the limit.</param>
/// <param name="ProposedBookValue">
/// Book value after the rebalance proposal (ADR-0013); equals <paramref name="BookValue"/> when the class does not move.
/// </param>
/// <param name="Share">
/// Book value / portfolio total, unrounded, as Compliance stored it (D-269). Consumers never recompute it.
/// </param>
public sealed record ClassEvaluation(
    AssetClassCode AssetClass, decimal BookValue, decimal Limit, decimal Utilization, ComplianceStatusCode Status,
    decimal ProposedBookValue, decimal Share)
{
    // The class only: never amounts or ratios (D-162).
    public override string ToString() => $"{nameof(ClassEvaluation)} {{ AssetClass = {AssetClass} }}";
}
