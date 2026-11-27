using FinGuard.Advisory.Features.Refinement;

namespace FinGuard.UnitTests.Advisory;

/// <summary>D-281, D-293: an outcome applies once, only while pending; the template narrative never changes.</summary>
public sealed class AdvisoryRefinementTests
{
    [Fact]
    public void ApplyRefinement_Pending_Accepted_AddsContextAndKeepsTemplate()
    {
        var advisory = Pending();

        var applied = advisory.ApplyRefinement(RefinementOutcome.Accepted, "model text", "fake-model");

        Assert.True(applied);
        Assert.Equal(
            ("template", "Template", "model text", "fake-model", RefinementStatus.Accepted, (string?)null),
            (advisory.Narrative, advisory.Narrator, advisory.AiContext, advisory.ModelId, advisory.RefinementStatus,
                advisory.RefinementReason));
    }

    [Fact]
    public void ApplyRefinement_Pending_Rejected_KeepsTemplateAndRecordsReason()
    {
        var advisory = Pending();

        advisory.ApplyRefinement(RefinementOutcome.StatusWording, null, null);

        Assert.Equal(
            ("template", (string?)null, (string?)null, RefinementStatus.Rejected, "StatusWording"),
            (advisory.Narrative, advisory.AiContext, advisory.ModelId, advisory.RefinementStatus, advisory.RefinementReason));
    }

    [Fact]
    public void ApplyRefinement_SecondDelivery_ChangesNothing()
    {
        var advisory = Pending();
        advisory.ApplyRefinement(RefinementOutcome.Timeout, null, null);

        var applied = advisory.ApplyRefinement(RefinementOutcome.Accepted, "model text", "fake-model");

        Assert.False(applied);
        Assert.Equal(((string?)null, RefinementStatus.Rejected), (advisory.AiContext, advisory.RefinementStatus));
    }

    [Fact]
    public void ApplyRefinement_NotRequested_ChangesNothing()
    {
        var advisory = Create();

        var applied = advisory.ApplyRefinement(RefinementOutcome.Accepted, "model text", "fake-model");

        Assert.False(applied);
        Assert.Equal(((string?)null, RefinementStatus.NotRequested), (advisory.AiContext, advisory.RefinementStatus));
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
