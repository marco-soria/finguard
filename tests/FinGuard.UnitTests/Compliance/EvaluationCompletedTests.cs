using FinGuard.Compliance.Contracts;
using FinGuard.Compliance.Contracts.Events;

namespace FinGuard.UnitTests.Compliance;

public sealed class EvaluationCompletedTests
{
    private const decimal MarkerAmount = 987654.32m;

    private const decimal MarkerProposed = 123456.78m;

    [Fact]
    public void ToString_Always_PrintsIdentifiersOnly()
    {
        var eventId = Guid.NewGuid();
        var evaluationId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var @event = new EvaluationCompleted(
            eventId, Guid.NewGuid(), DateTimeOffset.UtcNow, evaluationId, submissionId, new DateOnly(2026, 3, 31),
            ComplianceStatusCode.Breached, 1,
            [new ClassEvaluation(
                AssetClassCode.Shares, MarkerAmount, 0.25m, 1.5m, ComplianceStatusCode.Breached, MarkerProposed, 0.375m)],
            ProposalStatusCode.Full, 0.9m, 0.85m);

        var text = @event.ToString();

        Assert.Equal(
            $"EvaluationCompleted {{ EventId = {eventId}, EvaluationId = {evaluationId}, SubmissionId = {submissionId} }}",
            text);
        Assert.DoesNotContain("987654", text, StringComparison.Ordinal);
        Assert.DoesNotContain("123456", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Full", text, StringComparison.Ordinal);
        Assert.DoesNotContain("2026", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Breached", text, StringComparison.Ordinal);
        Assert.DoesNotContain("0.9", text, StringComparison.Ordinal);
        Assert.DoesNotContain("0.85", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassEvaluationToString_Always_PrintsClassOnly()
    {
        var text = new ClassEvaluation(
            AssetClassCode.Shares, MarkerAmount, 0.25m, 1.5m, ComplianceStatusCode.Breached, MarkerProposed, 0.375m).ToString();

        Assert.Equal("ClassEvaluation { AssetClass = Shares }", text);
    }
}
