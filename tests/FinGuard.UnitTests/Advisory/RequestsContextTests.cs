using FinGuard.Advisory.Features.WriteAdvisory;
using FinGuard.Compliance.Contracts;
using static FinGuard.UnitTests.Advisory.AdvisoryTestData;

namespace FinGuard.UnitTests.Advisory;

/// <summary>D-295: AI context is requested only with a model and a proposal to explain.</summary>
public sealed class RequestsContextTests
{
    [Fact]
    public void RequestsContext_BalancedProposalNotNeeded_IsFalse()
    {
        var facts = AdvisoryFactsBuilder.From(Balanced());

        Assert.Equal(
            (ProposalStatusCode.NotNeeded, false),
            (facts.ProposalStatus, EvaluationCompletedHandler.RequestsContext(facts, modelEnabled: true)));
    }

    [Theory]
    [InlineData("SharesHeavy")]
    [InlineData("WarningOnly")]
    public void RequestsContext_ProposalAndModel_IsTrue(string sample)
    {
        var facts = AdvisoryFactsBuilder.From(sample == "SharesHeavy" ? SharesHeavy() : WarningOnly());

        Assert.True(EvaluationCompletedHandler.RequestsContext(facts, modelEnabled: true));
    }

    [Fact]
    public void RequestsContext_NoModel_IsFalse()
    {
        Assert.False(EvaluationCompletedHandler.RequestsContext(AdvisoryFactsBuilder.From(SharesHeavy()), modelEnabled: false));
    }
}
