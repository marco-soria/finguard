using FinGuard.Advisory.Features.WriteAdvisory;
using FinGuard.Compliance.Contracts;
using static FinGuard.UnitTests.Advisory.AdvisoryTestData;

namespace FinGuard.UnitTests.Advisory;

public sealed class TemplateNarratorTests
{
    [Fact]
    public void Narrate_SharesHeavy_WritesTheGoldenText()
    {
        Assert.Equal(SharesHeavyNarrative, TemplateNarrator.Narrate(AdvisoryFactsBuilder.From(SharesHeavy())));
    }

    [Fact]
    public void Narrate_Balanced_WritesTheGoldenText()
    {
        Assert.Equal(BalancedNarrative, TemplateNarrator.Narrate(AdvisoryFactsBuilder.From(Balanced())));
    }

    [Fact]
    public void Narrate_WarningOnly_WritesTheGoldenText()
    {
        Assert.Equal(WarningOnlyNarrative, TemplateNarrator.Narrate(AdvisoryFactsBuilder.From(WarningOnly())));
    }

    [Fact]
    public void Narrate_Partial_EndsTheProposalWithTheRoundingSentence()
    {
        var facts = AdvisoryFactsBuilder.From(SharesHeavy()) with { ProposalStatus = ProposalStatusCode.Partial };

        var text = TemplateNarrator.Narrate(facts);

        Assert.Equal(
            SharesHeavyNarrative.Replace(
                "After these moves, every asset class is within its limit.",
                "Cent rounding leaves at least one asset class outside its target; review the amounts.",
                StringComparison.Ordinal),
            text);
    }

    [Fact]
    public void Narrate_BreachedAndWarning_OrdersBreachedFirstAndUsesPluralGrammar()
    {
        // cash 0.95 (Warning), shares 1.04 (Breached); receivers fixed income +44,767.45 and real estate +42,732.55.
        var facts = AdvisoryFactsBuilder.From(Event(380_000m, 200_000m, 260_000m, 160_000m));

        var paragraphs = TemplateNarrator.Narrate(facts).Split("\n\n");

        Assert.Equal("On 2026-06-30, the portfolio breaches the limit for Shares under policy version 1.", paragraphs[0]);
        Assert.Equal(
            "Shares holds 26.00 % of the portfolio against a limit of 25.00 %, so 104.00 % of the limit is used. " +
            "Cash holds 38.00 % of the portfolio against a limit of 40.00 %, so 95.00 % of the limit is used.",
            paragraphs[1]);
        Assert.StartsWith(
            "The proposal reduces Shares by $47,500.00 to $212,500.00 and Cash by $40,000.00 to $340,000.00, " +
            "each of which is 85.00 % of its limit. The amount moves to Fixed income (+$44,767.45) and Real estate " +
            "(+$42,732.55). Each receiving class moves toward 85.00 % of its limit.",
            paragraphs[2],
            StringComparison.Ordinal);
    }

    [Fact]
    public void Narrate_OneReceiver_UsesSingularGrammar()
    {
        var facts = AdvisoryFactsBuilder.From(WarningOnly());
        facts = facts with
        {
            Classes = facts.Classes.Select(c => c.Name == "Real estate" ? c with { Change = 0m } : c).ToList(),
        };

        var text = TemplateNarrator.Narrate(facts);

        Assert.Contains(
            "The amount moves to Fixed income (+$32,592.60). The receiving class moves toward 85.00 % of its limit.",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task NarrateAsync_SameFacts_WritesTheSameText()
    {
        var narrator = new TemplateNarrator();
        var facts = AdvisoryFactsBuilder.From(SharesHeavy());

        var first = await narrator.NarrateAsync(facts, TestContext.Current.CancellationToken);
        var second = await narrator.NarrateAsync(facts, TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
    }
}
