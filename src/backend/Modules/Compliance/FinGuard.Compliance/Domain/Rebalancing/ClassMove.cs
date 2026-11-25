namespace FinGuard.Compliance.Domain.Rebalancing;

/// <summary>One asset class before and after the proposal. <see cref="Change"/> is signed (negative = reduce).</summary>
public sealed record ClassMove(AssetClass Class, Money Current, Money Proposed)
{
    public decimal Change => Proposed.Amount - Current.Amount;

    // D-162: the class only, never amounts.
    public override string ToString() => $"{nameof(ClassMove)} {{ Class = {Class} }}";
}
