using FinGuard.Portfolio.Contracts;
using FinGuard.Portfolio.Contracts.Events;

namespace FinGuard.UnitTests.Portfolio;

public sealed class SubmissionAcceptedTests
{
    private const decimal MarkerAmount = 987654.32m;

    [Fact]
    public void ToString_Always_PrintsIdentifiersOnly()
    {
        var eventId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var @event = new SubmissionAccepted(
            eventId, Guid.NewGuid(), DateTimeOffset.UtcNow, submissionId, new DateOnly(2026, 3, 31), DateTimeOffset.UtcNow,
            [new AssetClassTotal(AssetClassCode.Cash, MarkerAmount)]);

        var text = @event.ToString();

        Assert.Equal($"SubmissionAccepted {{ EventId = {eventId}, SubmissionId = {submissionId} }}", text);
        Assert.DoesNotContain("987654", text, StringComparison.Ordinal);
        Assert.DoesNotContain("2026", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AssetClassTotalToString_Always_PrintsClassOnly()
    {
        var text = new AssetClassTotal(AssetClassCode.Shares, MarkerAmount).ToString();

        Assert.Equal("AssetClassTotal { AssetClass = Shares }", text);
    }
}
