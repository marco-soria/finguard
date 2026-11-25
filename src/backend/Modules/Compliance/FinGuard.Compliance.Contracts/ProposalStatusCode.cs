namespace FinGuard.Compliance.Contracts;

/// <summary>
/// Outcome of the rebalance proposal (ADR-0013): <c>NotNeeded</c> (Compliant, nothing moves), <c>Full</c> (after the
/// moves every class is Compliant) or <c>Partial</c> (cent rounding leaves a class outside its target). Consumers show
/// it and never recompute it. Additive changes only.
/// </summary>
public enum ProposalStatusCode
{
    NotNeeded,
    Full,
    Partial,
}
