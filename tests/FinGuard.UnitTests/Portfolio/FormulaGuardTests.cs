using FinGuard.Portfolio.Features.ReceiveSubmission.Csv;

namespace FinGuard.UnitTests.Portfolio;

public sealed class FormulaGuardTests
{
    [Theory]
    [InlineData("=SUM(A1)")]
    [InlineData("+1")]
    [InlineData("-x")]
    [InlineData("@cmd")]
    [InlineData("  =x")]
    [InlineData("\t=x")]
    [InlineData("\tx")]
    [InlineData("\rx")]
    [InlineData("\nx")]
    [InlineData("＝x")]
    [InlineData("＋x")]
    [InlineData("－x")]
    [InlineData("＠x")]
    [InlineData("=")]
    [InlineData(" \t=x")]
    [InlineData(" \tx")]
    [InlineData(" =x")]
    [InlineData("　@x")]
    public void StartsWithFormulaTrigger_Trigger_ReturnsTrue(string raw) =>
        Assert.True(FormulaGuard.StartsWithFormulaTrigger(raw));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Net - of fees")]
    [InlineData("a=b")]
    [InlineData("P-001")]
    [InlineData("x\t")]
    [InlineData("Café account")]
    [InlineData("(fictional) fund")]
    public void StartsWithFormulaTrigger_SafeValue_ReturnsFalse(string raw) =>
        Assert.False(FormulaGuard.StartsWithFormulaTrigger(raw));
}
