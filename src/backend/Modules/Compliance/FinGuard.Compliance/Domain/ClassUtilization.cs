namespace FinGuard.Compliance.Domain;

/// <summary>Utilization = (class value / total) / limit, all unrounded <c>decimal</c>.</summary>
public readonly record struct ClassUtilization(AssetClass Class, Money Value, Ratio Share, Ratio Limit, Ratio Utilization)
{
    public static ClassUtilization Calculate(AssetClass assetClass, Money value, Money total, Ratio limit)
    {
        var share = value.Amount / total.Amount;
        return new ClassUtilization(assetClass, value, new Ratio(share), limit, new Ratio(share / limit.Value));
    }

    // D-162: identifiers only.
    public override string ToString() => $"{nameof(ClassUtilization)} {{ Class = {Class} }}";
}
