namespace FinGuard.UnitTests.Advisory;

/// <summary>Model-style texts: a valid AI context paragraph and real model answers that must fail (D-293).</summary>
internal static class ModelTextTestData
{
    /// <summary>One paragraph, class names only: no figures, no symbols, no status wording.</summary>
    public const string ValidContext =
        "Holding too much of the portfolio in Shares concentrates its risk in one kind of asset, so a fall in that " +
        "market would weigh heavily on the whole portfolio. Moving Shares back to a fixed safety margin below its " +
        "maximum allocation spreads that money across Cash, Fixed income and Real estate, which keeps the portfolio " +
        "diversified and leaves room for market movements before any class reaches its maximum again.";

    /// <summary>
    /// Full rewrites from llama3.2:3b on 2026-11-27 that passed the first design's four rules but stated wrong class
    /// statuses (ADR-0015, D-293). The disclaimer, added by code, is left out.
    /// </summary>
    public static readonly string[] LiveRewrites =
    [
        // valid-balanced (Compliant): calls Shares and Real estate Breached or Warning and "above the limit".
        "The overall result of the portfolio compliance evaluation is Compliant.\n\nThe asset classes that are Breached or have a Warning status are Shares and Real estate.\n\nShares have a status of Compliant, but its limit is 25.00 % and limit used is 84.00 %, which is above the limit. Real estate has a status of Compliant, but its limit is 25.00 % and limit used is 88.00 %, which is above the limit.\n\nNo proposal is needed as the overall status is Compliant.\n\nA portfolio compliance evaluation result of Compliant means that the portfolio's asset allocation is within every limit.",
        // warning-cash (Warning): calls Fixed income Breached or Warning.
        "The overall result of the portfolio compliance evaluation is a Warning status, indicating that the portfolio is near the limit.\n\nThe asset classes that are Breached or have a Warning status are Cash and Fixed income.\n\nCash has a share of the portfolio of 38.00%, a limit of 40.00%, and is used to the extent of 95.00%. The proposed book value is $340,000.00, which is a reduction of $40,000.00 from the current book value of $380,000.00.\n\nFixed income has a share of the portfolio of 20.00%, a limit of 30.00%, and is used to the extent of 66.67%. The proposed book value is $232,592.60, which is an increase of $32,592.60 from the current book value of $200,000.00.",
        // warning-cash (Warning): calls Cash Breached and says its limit used "exceeds the limit".
        "The overall result of the portfolio compliance evaluation is a Warning status, indicating that the portfolio is near the limit.\n\nThe Cash asset class is Breached, with a share of the portfolio at 38.00% and a limit of 40.00%. The limit used is 95.00%, which exceeds the limit. The proposed book value is $340,000.00, which is $40,000.00 less than the current book value of $380,000.00.\n\nThe Fixed income asset class is Compliant, with a share of the portfolio at 20.00% and a limit of 30.00%. The limit used is 66.67%, which is within the limit. The proposed book value is $232,592.60, which is $32,592.60 more than the current book value of $200,000.00.\n\nThe Shares and Real estate asset classes are Compliant, with no proposed changes.\n\nThe proposal moves money to reduce the Cash asset class by $40,000.00, to $340,000.00.",
        // warning-cash (Warning): calls Cash Breached.
        "The overall result of the portfolio compliance evaluation is Warning, indicating that the portfolio is near the limit.\n\nThe Cash asset class is Breached, with a share of the portfolio of 38.00%, a limit of 40.00%, and 95.00% of the limit used. The proposed change is to reduce the book value by $40,000.00.\n\nThe Fixed income asset class is Compliant, with a share of the portfolio of 20.00%, a limit of 30.00%, and 66.67% of the limit used. The proposed change is to increase the book value by $32,592.60.\n\nThe Shares and Real estate asset classes are Compliant, with shares of the portfolio of 22.00% and 20.00%, respectively, limits of 25.00% and 25.00%, and 88.00% and 80.00% of the limits used, respectively. The proposed changes are to leave the book values unchanged for Shares and to increase the book value by $7,407.40 for Real estate.",
    ];
}
