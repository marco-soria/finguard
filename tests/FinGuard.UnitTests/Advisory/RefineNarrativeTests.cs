using FinGuard.Advisory.Features.Refinement;
using FinGuard.Advisory.Features.WriteAdvisory;
using static FinGuard.UnitTests.Advisory.AdvisoryTestData;
using static FinGuard.UnitTests.Advisory.ModelTextTestData;

namespace FinGuard.UnitTests.Advisory;

/// <summary>D-281, D-282: every expected model failure becomes a rejection reason; nothing escapes.</summary>
public sealed class RefineNarrativeTests
{
    private static readonly AdvisoryFacts Facts = AdvisoryFactsBuilder.From(SharesHeavy());
    private static readonly Guid AdvisoryId = Guid.NewGuid();

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RefineAsync_ValidText_AcceptsWithDisclaimerAndModel()
    {
        var result = await Refine(_ => Task.FromResult(SharesHeavyParaphrase.Replace("\n\n", "\r\n\r\n", StringComparison.Ordinal)));

        Assert.Equal(
            new StoreRefinedNarrative(AdvisoryId, RefinementOutcome.Accepted, SharesHeavyParaphrase + "\n\n" + Disclaimer, "fake-model"),
            result);
    }

    public static TheoryData<string, RefinementOutcome> RejectedTexts => new()
    {
        { SharesHeavyParaphrase.Replace("$637,500.00", "$637,600.00", StringComparison.Ordinal), RefinementOutcome.UnknownNumber },
        { SharesHeavyParaphrase.Replace("breaches", "exceeds", StringComparison.Ordinal), RefinementOutcome.StatusMismatch },
        { SharesHeavyParaphrase.Replace("Shares make up", "**Shares** make up", StringComparison.Ordinal), RefinementOutcome.Format },
        { "Shares breaches its limit.", RefinementOutcome.Length },
    };

    [Theory]
    [MemberData(nameof(RejectedTexts))]
    public async Task RefineAsync_InvalidText_RejectsWithItsReason(string text, RefinementOutcome expected)
    {
        var result = await Refine(_ => Task.FromResult(text));

        Assert.Equal(new StoreRefinedNarrative(AdvisoryId, expected, null, null), result);
    }

    [Fact]
    public async Task RefineAsync_NarratorTimesOut_RejectsWithTimeout()
    {
        var result = await Refine(_ => throw new TimeoutException());

        Assert.Equal(RefinementOutcome.Timeout, result.Outcome);
    }

    [Fact]
    public async Task RefineAsync_ModelThrows_RejectsWithModelError()
    {
        var result = await Refine(_ => throw new HttpRequestException("connection refused"));

        Assert.Equal(new StoreRefinedNarrative(AdvisoryId, RefinementOutcome.ModelError, null, null), result);
    }

    [Fact]
    public async Task RefineAsync_HostShutsDown_LetsCancellationEscape()
    {
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RefineNarrativeHandler.RefineAsync(
            AdvisoryId, Facts, new StubNarrator(ct => throw new OperationCanceledException(ct)), shutdown.Token));
    }

    [Fact]
    public void StoreRefinedNarrative_ToString_CarriesNoText()
    {
        var message = new StoreRefinedNarrative(AdvisoryId, RefinementOutcome.Accepted, SharesHeavyParaphrase, "fake-model");

        Assert.Equal($"StoreRefinedNarrative {{ AdvisoryId = {AdvisoryId}, Outcome = Accepted }}", message.ToString());
    }

    private Task<StoreRefinedNarrative> Refine(Func<CancellationToken, Task<string>> narrate) =>
        RefineNarrativeHandler.RefineAsync(AdvisoryId, Facts, new StubNarrator(narrate), _ct);

    private sealed class StubNarrator(Func<CancellationToken, Task<string>> narrate) : IAdvisoryNarrator
    {
        public string? ModelId => "fake-model";

        public Task<string> NarrateAsync(AdvisoryFacts facts, CancellationToken ct) => narrate(ct);
    }
}
