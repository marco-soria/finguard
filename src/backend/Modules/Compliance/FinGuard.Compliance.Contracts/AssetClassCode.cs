namespace FinGuard.Compliance.Contracts;

/// <summary>
/// The asset classes in Compliance's published contracts (ADR-0011 rule). Compliance owns this copy, so its contracts
/// never reference another module's contracts (D-196). Additive changes only: a new member is a contract change that
/// every consumer must handle.
/// </summary>
public enum AssetClassCode
{
    Cash,
    FixedIncome,
    Shares,
    RealEstate,
}
