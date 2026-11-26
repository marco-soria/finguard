using FinGuard.Advisory.Features.WriteAdvisory;
using static FinGuard.UnitTests.Advisory.AdvisoryTestData;

namespace FinGuard.UnitTests.Advisory;

public sealed class NarrativeValidatorTests
{
    public static TheoryData<string> GoldenTexts => [SharesHeavyNarrative, BalancedNarrative, WarningOnlyNarrative];

    [Theory]
    [MemberData(nameof(GoldenTexts))]
    public void Validate_GoldenText_Succeeds(string narrative)
    {
        var facts = narrative == BalancedNarrative ? AdvisoryFactsBuilder.From(Balanced())
            : narrative == WarningOnlyNarrative ? AdvisoryFactsBuilder.From(WarningOnly())
            : AdvisoryFactsBuilder.From(SharesHeavy());

        Assert.True(NarrativeValidator.Validate(narrative, facts).IsSuccess);
    }

    [Theory]
    [InlineData("$1,562,500.00", "$1,562,600.00")] // a changed amount
    [InlineData("85.00 % of its limit.", "85.00 % of its limit, or 12.50 % of the total.")] // an extra percentage
    [InlineData("policy version 1.", "policy version 2.")] // a number not in the facts
    public void Validate_NumberNotInFacts_FailsWithoutNamingTheNumber(string original, string replacement)
    {
        var narrative = SharesHeavyNarrative.Replace(original, replacement, StringComparison.Ordinal);
        Assert.NotEqual(SharesHeavyNarrative, narrative);

        var result = NarrativeValidator.Validate(narrative, AdvisoryFactsBuilder.From(SharesHeavy()));

        Assert.Equal(NarrativeValidator.UnknownNumber, result.Error);
        var newNumber = replacement.Split(' ').First(w => w.Any(char.IsDigit)).Trim('$', '.', ',');
        Assert.DoesNotContain(newNumber, result.Error!.Description, StringComparison.Ordinal);
        Assert.DoesNotContain(newNumber, result.Error.Code, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_TextWithoutNumbers_Succeeds()
    {
        Assert.True(NarrativeValidator.Validate(Disclaimer, AdvisoryFactsBuilder.From(Balanced())).IsSuccess);
    }
}
