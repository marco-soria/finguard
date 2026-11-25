namespace FinGuard.Compliance.Domain.Rebalancing;

/// <summary>Outcome of the rebalance engine (ADR-0013).</summary>
public enum ProposalStatus
{
    /// <summary>The evaluation is Compliant: nothing moves.</summary>
    NotNeeded,

    /// <summary>After the moves every class is Compliant.</summary>
    Full,

    /// <summary>Verification found a class that is still not Compliant (reachable only through cent rounding).</summary>
    Partial,
}
