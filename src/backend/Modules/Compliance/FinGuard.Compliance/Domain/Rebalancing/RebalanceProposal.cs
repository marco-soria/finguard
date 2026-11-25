namespace FinGuard.Compliance.Domain.Rebalancing;

/// <summary>Exactly one <see cref="ClassMove"/> per <see cref="AssetClass"/>, in enum order; the total is preserved.</summary>
public sealed record RebalanceProposal(ProposalStatus Status, IReadOnlyList<ClassMove> Moves)
{
    // D-162: no amounts.
    public override string ToString() => nameof(RebalanceProposal);
}
