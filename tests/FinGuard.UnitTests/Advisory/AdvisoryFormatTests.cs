using FinGuard.Advisory.Features.WriteAdvisory;
using FinGuard.Compliance.Contracts;

namespace FinGuard.UnitTests.Advisory;

// Same vectors as format-ratio.spec.ts and usd.pipe.spec.ts in the SPA (D-252): change both together.
public sealed class AdvisoryFormatTests
{
    [Theory]
    [InlineData("0.89999996", ComplianceStatusCode.Compliant, "89.999996 %")]
    [InlineData("0.9", ComplianceStatusCode.Warning, "90.00 %")]
    [InlineData("1", ComplianceStatusCode.Warning, "100.00 %")]
    [InlineData("1.00000001", ComplianceStatusCode.Breached, "100.000001 %")]
    [InlineData("2.9333333333333333333333333333", ComplianceStatusCode.Breached, "293.33 %")]
    public void Ratio_SharedVector_MatchesTheSpa(string utilization, ComplianceStatusCode status, string expected)
    {
        var text = AdvisoryFormat.Ratio(decimal.Parse(utilization, System.Globalization.CultureInfo.InvariantCulture), status, 0.90m);

        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData("0.25", "25.00 %")]
    [InlineData("0.85", "85.00 %")]
    [InlineData("0.7333333333333333333333333333", "73.33 %")]
    public void Percent_SharedVector_MatchesTheSpa(string ratio, string expected)
    {
        Assert.Equal(expected, AdvisoryFormat.Percent(decimal.Parse(ratio, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("1562500", "$1,562,500.00")]
    [InlineData("0.01", "$0.01")]
    [InlineData("0", "$0.00")]
    [InlineData("693374.43", "$693,374.43")]
    public void Money_SharedVector_MatchesTheSpa(string amount, string expected)
    {
        Assert.Equal(expected, AdvisoryFormat.Money(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
