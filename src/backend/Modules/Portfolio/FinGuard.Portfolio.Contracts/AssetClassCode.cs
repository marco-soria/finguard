namespace FinGuard.Portfolio.Contracts;

/// <summary>
/// The asset classes of a portfolio, as published to other modules (ADR-0011). Consumers map it to their own types.
/// Additive changes only: a new member is a contract change that every consumer must handle.
/// </summary>
public enum AssetClassCode
{
    Cash,
    FixedIncome,
    Shares,
    RealEstate,
}
