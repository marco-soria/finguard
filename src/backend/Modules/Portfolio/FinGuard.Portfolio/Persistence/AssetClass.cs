namespace FinGuard.Portfolio.Persistence;

/// <summary>The asset classes of AGENTS.md. Stored and read as their lower-case CSV codes.</summary>
internal enum AssetClass
{
    Cash,
    FixedIncome,
    Shares,
    RealEstate,
}

internal static class AssetClassCodes
{
    public const int MaxLength = 20;

    public static string ToCode(this AssetClass assetClass) => assetClass switch
    {
        AssetClass.Cash => "cash",
        AssetClass.FixedIncome => "fixed_income",
        AssetClass.Shares => "shares",
        AssetClass.RealEstate => "real_estate",
        _ => throw new ArgumentOutOfRangeException(nameof(assetClass), assetClass, null),
    };

    public static AssetClass FromCode(string code) =>
        TryParse(code, out var assetClass) ? assetClass : throw new ArgumentOutOfRangeException(nameof(code));

    /// <summary>Case-insensitive; the caller trims.</summary>
    public static bool TryParse(string code, out AssetClass assetClass)
    {
        foreach (var candidate in Enum.GetValues<AssetClass>())
        {
            if (string.Equals(candidate.ToCode(), code, StringComparison.OrdinalIgnoreCase))
            {
                assetClass = candidate;
                return true;
            }
        }

        assetClass = default;
        return false;
    }
}
