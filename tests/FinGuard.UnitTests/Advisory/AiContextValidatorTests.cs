using System.Text.RegularExpressions;
using FinGuard.Advisory.Features.Refinement;
using static FinGuard.UnitTests.Advisory.ModelTextTestData;

namespace FinGuard.UnitTests.Advisory;

/// <summary>D-293: the AI context paragraph has no figures, no symbols, no status wording; plain text, one paragraph.</summary>
public sealed partial class AiContextValidatorTests
{
    [Fact]
    public void Validate_ClassNamesOnlyParagraph_Succeeds()
    {
        Assert.True(AiContextValidator.Validate(ValidContext).IsSuccess);
    }

    [Theory]
    [InlineData("It moves money from 1 class.")]
    [InlineData("It moves money from one class to 3.")]
    [InlineData("Shares fell in 2008.")]
    public void Validate_Digit_FailsWithDigits(string sentence)
    {
        Assert.Equal(AiContextValidator.Digits, AiContextValidator.Validate(ValidContext + " " + sentence).Error);
    }

    [Theory]
    [InlineData("A large % of the portfolio sits in Shares.")]
    [InlineData("It moves $ out of Shares.")]
    [InlineData("It moves € out of Shares.")]
    [InlineData("Shares hold most of the money, in percent terms.")]
    [InlineData("A percentage of Shares moves to Cash.")]
    [InlineData("Shares hold most of it, per cent.")]
    [InlineData("It moves thousands of dollars.")]
    [InlineData("It moves a dollar.")]
    [InlineData("Everything is in USD.")]
    [InlineData("Down to the last cent.")]
    public void Validate_AmountOrPercentSymbolOrWord_FailsWithSymbols(string sentence)
    {
        Assert.Equal(AiContextValidator.Symbols, AiContextValidator.Validate(ValidContext + " " + sentence).Error);
    }

    [Theory]
    [InlineData("Shares are in breach.")]
    [InlineData("Shares breached.")]
    [InlineData("Shares breaches.")]
    [InlineData("Shares exceeds the maximum.")]
    [InlineData("Shares exceeded the maximum.")]
    [InlineData("Shares sits above the limit.")]
    [InlineData("Cash raises a warning.")]
    [InlineData("Cash sits near the limit.")]
    [InlineData("Cash is compliant.")]
    [InlineData("Cash is non-compliant.")]
    [InlineData("Cash is COMPLIANT.")]
    [InlineData("Cash stays within the limit.")]
    [InlineData("Most of the limit used is in Cash.")]
    public void Validate_StatusWording_FailsWithStatusWording(string sentence)
    {
        Assert.Equal(AiContextValidator.StatusWording, AiContextValidator.Validate(ValidContext + " " + sentence).Error);
    }

    [Theory]
    [InlineData("**Shares**")]
    [InlineData("# Summary")]
    [InlineData("<b>Shares</b>")]
    [InlineData("`Shares`")]
    [InlineData("| Shares |")]
    [InlineData("See https://example.com for details.")]
    [InlineData("See www.example.com for details.")]
    public void Validate_Markup_FailsWithFormat(string markup)
    {
        var result = AiContextValidator.Validate(ValidContext + " " + markup);

        Assert.Equal(AiContextValidator.Format, result.Error);
        Assert.DoesNotContain(markup, result.Error!.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ShorterThan150_FailsWithLength()
    {
        Assert.Equal(AiContextValidator.Length, AiContextValidator.Validate(ValidContext[..149]).Error);
    }

    [Fact]
    public void Validate_Exactly150Or700_Succeeds()
    {
        var long700 = (ValidContext + string.Concat(Enumerable.Repeat(" More context.", 40)))[..700];

        Assert.Equal((true, true), (
            AiContextValidator.Validate(ValidContext[..150]).IsSuccess, AiContextValidator.Validate(long700).IsSuccess));
    }

    [Fact]
    public void Validate_LongerThan700_FailsWithLength()
    {
        var text = (ValidContext + string.Concat(Enumerable.Repeat(" More context.", 40)))[..701];

        Assert.Equal(AiContextValidator.Length, AiContextValidator.Validate(text).Error);
    }

    [Fact]
    public void Validate_TwoParagraphs_FailsWithLength()
    {
        var text = ValidContext[..200] + "\n\n" + ValidContext[..200];

        Assert.Equal(AiContextValidator.Length, AiContextValidator.Validate(text).Error);
    }

    public static TheoryData<int> LiveRewriteIndexes => new(Enumerable.Range(0, LiveRewrites.Length));

    [Theory]
    [MemberData(nameof(LiveRewriteIndexes))]
    public void Validate_LiveRewrite_FailsWithDigits(int index)
    {
        var text = AiContextValidator.Normalize(LiveRewrites[index]);

        Assert.Equal(AiContextValidator.Digits, AiContextValidator.Validate(text).Error);
    }

    [Theory]
    [MemberData(nameof(LiveRewriteIndexes))]
    public void Validate_LiveRewriteWithoutFigures_FailsWithStatusWording(int index)
    {
        // With every digit, $ and % removed, the wrong class statuses are still caught.
        var text = AiContextValidator.Normalize(Figures().Replace(LiveRewrites[index], string.Empty));

        Assert.Equal(AiContextValidator.StatusWording, AiContextValidator.Validate(text).Error);
    }

    [Fact]
    public void Normalize_CrLfAndLineBreaks_OnlyChangesWhitespace()
    {
        const string text = "  First line\r\nsecond line.\r\n\r\n\r\n  Next paragraph.  ";

        Assert.Equal("First line second line.\n\nNext paragraph.", AiContextValidator.Normalize(text));
    }

    [GeneratedRegex(@"[\d$%]")]
    private static partial Regex Figures();
}
