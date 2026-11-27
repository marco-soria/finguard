using FinGuard.Advisory.Features.Refinement;

namespace FinGuard.UnitTests.Advisory;

/// <summary>D-281: an outcome applies once, only while the refinement is pending.</summary>
public sealed class AdvisoryRefinementTests
{
    [Fact]
    public void ApplyRefinement_Pending_Accepted_ReplacesNarrative()
    {
        var advisory = Pending();

        var applied = advisory.ApplyRefinement(RefinementOutcome.Accepted, "model text", "fake-model");

        Assert.True(applied);
        Assert.Equal(
            ("model text", "Model", "fake-model", RefinementStatus.Accepted, (string?)null),
            (advisory.Narrative, advisory.Narrator, advisory.ModelId, advisory.RefinementStatus, advisory.RefinementReason));
    }

    [Fact]
    public void ApplyRefinement_Pending_Rejected_KeepsTemplateAndRecordsReason()
    {
        var advisory = Pending();

        advisory.ApplyRefinement(RefinementOutcome.UnknownNumber, null, null);

        Assert.Equal(
            ("template", "Template", (string?)null, RefinementStatus.Rejected, "UnknownNumber"),
            (advisory.Narrative, advisory.Narrator, advisory.ModelId, advisory.RefinementStatus, advisory.RefinementReason));
    }

    [Fact]
    public void ApplyRefinement_SecondDelivery_ChangesNothing()
    {
        var advisory = Pending();
        advisory.ApplyRefinement(RefinementOutcome.Timeout, null, null);

        var applied = advisory.ApplyRefinement(RefinementOutcome.Accepted, "model text", "fake-model");

        Assert.False(applied);
        Assert.Equal(("template", RefinementStatus.Rejected), (advisory.Narrative, advisory.RefinementStatus));
    }

    [Fact]
    public void ApplyRefinement_NotRequested_ChangesNothing()
    {
        var advisory = Create();

        var applied = advisory.ApplyRefinement(RefinementOutcome.Accepted, "model text", "fake-model");

        Assert.False(applied);
        Assert.Equal(("template", RefinementStatus.NotRequested), (advisory.Narrative, advisory.RefinementStatus));
    }

    private static FinGuard.Advisory.Persistence.Advisory Create() =>
        FinGuard.Advisory.Persistence.Advisory.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "{}", "template", "Template", DateTimeOffset.UnixEpoch);

    private static FinGuard.Advisory.Persistence.Advisory Pending()
    {
        var advisory = Create();
        advisory.RequestRefinement();
        return advisory;
    }
}
