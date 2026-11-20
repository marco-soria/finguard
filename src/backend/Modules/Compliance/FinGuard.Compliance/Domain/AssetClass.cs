namespace FinGuard.Compliance.Domain;

/// <summary>The four asset classes the limits apply to. Owned by the domain; <c>Features/</c> maps the contract code (D-172).</summary>
public enum AssetClass
{
    Cash,
    FixedIncome,
    Shares,
    RealEstate,
}
