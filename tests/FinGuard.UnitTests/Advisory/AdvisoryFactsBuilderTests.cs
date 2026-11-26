using FinGuard.Advisory.Features.WriteAdvisory;
using FinGuard.Compliance.Contracts;
using static FinGuard.UnitTests.Advisory.AdvisoryTestData;

namespace FinGuard.UnitTests.Advisory;

public sealed class AdvisoryFactsBuilderTests
{
    [Fact]
    public void From_SharesHeavy_CarriesFourClassesInEnumOrderWithTheirTexts()
    {
        var @event = SharesHeavy();

        var facts = AdvisoryFactsBuilder.From(@event);

        Assert.Equal(
            (@event.EvaluationId, @event.SubmissionId, "2026-06-30", "1", ComplianceStatusCode.Breached,
             ProposalStatusCode.Full, 0.90m, 0.85m, "85.00 %"),
            (facts.EvaluationId, facts.SubmissionId, facts.CutOffDateText, facts.PolicyVersionText, facts.OverallStatus,
             facts.ProposalStatus, facts.AlertThreshold, facts.RebalanceTargetRatio, facts.TargetRatioText));
        Assert.Equal(Enum.GetValues<AssetClassCode>(), facts.Classes.Select(c => c.AssetClass));
        Assert.Equal(["Cash", "Fixed income", "Shares", "Real estate"], facts.Classes.Select(c => c.Name));
        var shares = facts.Classes[2];
        Assert.Equal(
            ("$2,200,000.00", "73.33 %", "25.00 %", "293.33 %", "$637,500.00", "$1,562,500.00", -1_562_500m),
            (shares.BookValueText, shares.ShareText, shares.LimitText, shares.UtilizationText, shares.ProposedText,
             shares.ChangeText, shares.Change));
        var cash = facts.Classes[0];
        Assert.Equal(("$993,374.43", "$693,374.43", 693_374.43m), (cash.ProposedText, cash.ChangeText, cash.Change));
    }

    [Fact]
    public void From_Event_UsesTheSharesCompliancePublished()
    {
        // D-269: Advisory formats the event's share; it never divides book value by a total of its own.
        var @event = SharesHeavy();
        var marked = @event with
        {
            Classes = @event.Classes.Select(c => c with { Share = c.AssetClass == AssetClassCode.Shares ? 0.5m : c.Share }).ToList(),
        };

        var facts = AdvisoryFactsBuilder.From(marked);

        Assert.Equal((0.5m, "50.00 %"), (facts.Classes[2].Share, facts.Classes[2].ShareText));
    }

    [Fact]
    public void From_EventWithMissingClass_Throws()
    {
        var @event = SharesHeavy();

        Assert.Throws<InvalidOperationException>(
            () => AdvisoryFactsBuilder.From(@event with { Classes = @event.Classes.Skip(1).ToList() }));
    }

    [Fact]
    public void ToString_Always_PrintsIdentifiersOnly()
    {
        var facts = AdvisoryFactsBuilder.From(SharesHeavy());

        Assert.Equal(
            $"AdvisoryFacts {{ EvaluationId = {facts.EvaluationId}, SubmissionId = {facts.SubmissionId} }}",
            facts.ToString());
        Assert.Equal("ClassFacts { AssetClass = Shares }", facts.Classes[2].ToString());
    }
}
