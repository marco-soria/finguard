namespace FinGuard.Portfolio.Contracts;

/// <summary>Book value in USD of one asset class in a <c>SubmissionAccepted</c> event (ADR-0010, ADR-0011).</summary>
public sealed record AssetClassTotal(AssetClassCode AssetClass, decimal BookValue)
{
    // Never the amount (D-77).
    public override string ToString() => $"{nameof(AssetClassTotal)} {{ AssetClass = {AssetClass} }}";
}
