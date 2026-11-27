using FinGuard.Advisory.Features.WriteAdvisory;
using static FinGuard.UnitTests.Advisory.AdvisoryTestData;
using static FinGuard.UnitTests.Advisory.ModelTextTestData;

namespace FinGuard.UnitTests.Advisory;

/// <summary>D-283: the four rules for model text (numbers, status phrase, plain text, length).</summary>
public sealed class NarrativeValidatorModelTextTests
{
    private static readonly AdvisoryFacts Facts = AdvisoryFactsBuilder.From(SharesHeavy());

    [Fact]
    public void ValidateModelText_CorrectParaphrase_Succeeds()
    {
        var result = NarrativeValidator.ValidateModelText(SharesHeavyParaphrase, Facts);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("$1,562,500.00", "$1,562,600.00")] // a changed amount
    [InlineData("85.00 % of its limit.", "85.00 % of its limit, or 12.50 % of the total.")] // a percentage not in the facts
    public void ValidateModelText_NumberNotInFacts_FailsWithUnknownNumber(string original, string replacement)
    {
        var text = Replace(original, replacement);

        var result = NarrativeValidator.ValidateModelText(text, Facts);

        Assert.Equal(NarrativeValidator.UnknownNumber, result.Error);
    }

    [Theory]
    [InlineData("breaches its limit for Shares", "is within every limit, but Shares breaches its limit,")] // other status phrase
    [InlineData("breaches its limit for Shares", "exceeds its limit for Shares")] // own phrase missing
    [InlineData("Shares make up", "Shares, near the limit, make up")]
    public void ValidateModelText_WrongStatusPhrase_FailsWithStatusMismatch(string original, string replacement)
    {
        var text = Replace(original, replacement);

        var result = NarrativeValidator.ValidateModelText(text, Facts);

        Assert.Equal(NarrativeValidator.StatusMismatch, result.Error);
    }

    [Theory]
    [InlineData("**Shares**")]
    [InlineData("# Summary")]
    [InlineData("<b>Shares</b>")]
    [InlineData("`Shares`")]
    [InlineData("| Shares |")]
    [InlineData("See https://example.com for details.")]
    [InlineData("See www.example.com for details.")]
    public void ValidateModelText_Markup_FailsWithFormat(string markup)
    {
        var text = SharesHeavyParaphrase + " " + markup;

        var result = NarrativeValidator.ValidateModelText(text, Facts);

        Assert.Equal(NarrativeValidator.Format, result.Error);
        Assert.DoesNotContain(markup, result.Error!.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateModelText_ShorterThan200_FailsWithLength()
    {
        var text = "On 2026-06-30, the portfolio breaches its limit for Shares.";

        var result = NarrativeValidator.ValidateModelText(text, Facts);

        Assert.Equal(NarrativeValidator.Length, result.Error);
    }

    [Fact]
    public void ValidateModelText_LongerThan1500_FailsWithLength()
    {
        var text = SharesHeavyParaphrase + " " + string.Concat(Enumerable.Repeat("The proposal is deterministic. ", 40));

        var result = NarrativeValidator.ValidateModelText(text, Facts);

        Assert.True(text.Length > NarrativeValidator.MaxModelLength);
        Assert.Equal(NarrativeValidator.Length, result.Error);
    }

    [Fact]
    public void ValidateModelText_SixParagraphs_FailsWithLength()
    {
        var text = SharesHeavyParaphrase + string.Concat(Enumerable.Repeat("\n\nShares breaches its limit.", 4));

        var result = NarrativeValidator.ValidateModelText(text, Facts);

        Assert.Equal(NarrativeValidator.Length, result.Error);
    }

    [Fact]
    public void ValidateModelText_AnyFailure_NeverEchoesTheText()
    {
        var text = Replace("$1,562,500.00", "$1,562,600.00");

        var error = NarrativeValidator.ValidateModelText(text, Facts).Error!;

        Assert.DoesNotContain("1,562,600", error.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("1,562,600", error.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeModelText_CrLfAndLineBreaks_KeepsWordsAndUsesBlankLines()
    {
        var raw = "  First line\r\nsame paragraph.\r\n\r\n\r\n  Second   paragraph.  \n";

        var normalized = NarrativeValidator.NormalizeModelText(raw);

        Assert.Equal("First line same paragraph.\n\nSecond   paragraph.", normalized);
    }

    private static string Replace(string original, string replacement)
    {
        var text = SharesHeavyParaphrase.Replace(original, replacement, StringComparison.Ordinal);
        Assert.NotEqual(SharesHeavyParaphrase, text);
        return text;
    }
}
