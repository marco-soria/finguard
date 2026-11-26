using System.Globalization;
using FinGuard.Compliance.Contracts;

namespace FinGuard.Advisory.Features.WriteAdvisory;

/// <summary>
/// The SPA's display rules in C# (D-252): <c>UsdPipe</c>, <c>formatPercent</c> and <c>formatRatio</c> (D-207, D-208).
/// Both test suites share the same vectors. Invariant culture, so the output does not depend on the host's ICU data.
/// </summary>
public static class AdvisoryFormat
{
    private const int MinDecimals = 2;
    private const int MaxDecimals = 8;

    /// <summary>en-US USD with 2 decimals: <c>$1,562,500.00</c>. Amounts are never negative here.</summary>
    public static string Money(decimal amount) =>
        "$" + Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>A ratio as a percentage with 2 decimals: <c>0.25</c> is <c>25.00 %</c>.</summary>
    public static string Percent(decimal ratio) => Fixed(ratio * 100m, MinDecimals) + " %";

    /// <summary>
    /// A utilization with 2 to 8 decimals, adding decimals while the text would read as another status
    /// (<c>0.89999996</c> Compliant is <c>89.999996 %</c>, never <c>90.00 %</c>). It only formats.
    /// </summary>
    public static string Ratio(decimal utilization, ComplianceStatusCode status, decimal alertThreshold)
    {
        var threshold = alertThreshold * 100m;
        var text = string.Empty;
        for (var decimals = MinDecimals; decimals <= MaxDecimals; decimals++)
        {
            text = Fixed(utilization * 100m, decimals);
            if (Agrees(decimal.Parse(text, CultureInfo.InvariantCulture), status, threshold))
            {
                break;
            }
        }

        return text + " %";
    }

    // JavaScript's toFixed rounds a midpoint up for positive values; R-48 covers double vs decimal differences.
    private static string Fixed(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero)
            .ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static bool Agrees(decimal shown, ComplianceStatusCode status, decimal threshold) => status switch
    {
        ComplianceStatusCode.Compliant => shown < threshold,
        ComplianceStatusCode.Warning => shown >= threshold && shown <= 100m,
        ComplianceStatusCode.Breached => shown > 100m,
        _ => true,
    };
}
