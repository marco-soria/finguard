using FinGuard.Advisory;
using FinGuard.Advisory.Features.WriteAdvisory;
using FinGuard.Advisory.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using static FinGuard.UnitTests.Advisory.AdvisoryTestData;

namespace FinGuard.UnitTests.Advisory;

public sealed class AgentFrameworkNarratorTests
{
    private static readonly AdvisoryFacts Facts = AdvisoryFactsBuilder.From(SharesHeavy());

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task NarrateAsync_FakeModel_ReturnsItsText()
    {
        var narrator = Narrator(FakeChatClient.Answering("model text"), new FakeTimeProvider());

        var text = await narrator.NarrateAsync(Facts, _ct);

        Assert.Equal(("model text", "fake-model"), (text, narrator.ModelId));
    }

    [Fact]
    public async Task NarrateAsync_Always_SendsTheFactsJsonAndTheFixedInstructionsOnly()
    {
        // Prompt safety: the only user content is the facts' JSON, which has no free text (D-250).
        var client = FakeChatClient.Answering("model text");

        await Narrator(client, new FakeTimeProvider()).NarrateAsync(Facts, _ct);

        var user = Assert.Single(client.LastMessages, m => m.Role == ChatRole.User);
        Assert.Equal(NarratorPrompt.Input(Facts), user.Text);
        Assert.Equal(NarratorPrompt.Instructions, client.LastOptions?.Instructions);
        Assert.True(client.LastOptions?.Tools is null or { Count: 0 });
    }

    [Fact]
    public async Task NarrateAsync_ModelSlowerThanTimeout_ThrowsTimeoutException()
    {
        var time = new FakeTimeProvider();
        var client = new FakeChatClient(async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return "never";
        });
        var narrate = Narrator(client, time).NarrateAsync(Facts, _ct);

        time.Advance(TimeSpan.FromSeconds(91));

        await Assert.ThrowsAsync<TimeoutException>(() => narrate);
    }

    [Fact]
    public async Task NarrateAsync_CallerCancels_ThrowsOperationCanceled()
    {
        using var caller = new CancellationTokenSource();
        var client = new FakeChatClient(async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return "never";
        });
        var narrate = Narrator(client, new FakeTimeProvider()).NarrateAsync(Facts, caller.Token);

        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => narrate);
    }

    private static AgentFrameworkNarrator Narrator(IChatClient client, TimeProvider time) =>
        new(client, Options.Create(new AdvisoryOptions { TimeoutSeconds = 90 }), time);
}
