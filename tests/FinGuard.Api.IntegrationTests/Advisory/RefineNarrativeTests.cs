using System.Net;
using System.Net.Http.Json;
using Dapper;
using FinGuard.Advisory.Features.GetAdvisory;
using FinGuard.Advisory.Features.Refinement;
using FinGuard.Api.IntegrationTests.Portfolio;
using FinGuard.Portfolio.Features.ReceiveSubmission;
using FinGuard.Tenancy.Development;
using Npgsql;
using Wolverine.Tracking;

namespace FinGuard.Api.IntegrationTests.Advisory;

/// <summary>
/// Brief #19 (D-281, D-282, D-293): the template narrative is always stored and never changes. The model adds one
/// context paragraph only when it passes validation; every expected failure records a reason and leaves no dead
/// letter.
/// </summary>
public sealed class RefineNarrativeTests(ModelNarratorHost host) : IClassFixture<ModelNarratorHost>
{
    // One paragraph, class names only: no figures, no symbols, no status wording.
    private const string Context =
        "Holding too much of the portfolio in Shares concentrates its risk in one kind of asset, so a fall in that " +
        "market would weigh heavily on the whole portfolio. Moving Shares back to a fixed safety margin below its " +
        "maximum allocation spreads that money across Cash, Fixed income and Real estate, which keeps the portfolio " +
        "diversified and leaves room for market movements before any class reaches its maximum again.";

    // A first-design rewrite: figures and class statuses, which the context paragraph may not contain.
    private const string Rewrite =
        "The overall result of the portfolio compliance evaluation is Breached. Shares make up 73.33 % of the " +
        "portfolio while the limit is 25.00 %, so 293.33 % of the limit is used, which exceeds the limit.";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_SharesHeavy_ContextAccepted_KeepsTemplateAndAddsContext()
    {
        host.ChatClient.Respond = _ => Task.FromResult(Context);

        var advisory = await UploadAndReadAsync("2026-06-30");

        AssertTemplate(advisory, "2026-06-30");
        Assert.Equal(("fake-model", "Accepted", Context), (advisory.Model, advisory.Refinement, advisory.AiContext));
        Assert.Equal((0L, "Accepted", (string?)null), await StoredStateAsync(advisory.SubmissionId));
    }

    [Fact]
    public async Task Upload_SharesHeavy_ModelWritesFiguresAndStatuses_RejectsContext()
    {
        host.ChatClient.Respond = _ => Task.FromResult(Rewrite);

        var advisory = await UploadAndReadAsync("2026-05-31");

        AssertNoContext(advisory, "2026-05-31");
        Assert.Equal((0L, "Rejected", "Digits"), await StoredStateAsync(advisory.SubmissionId));
    }

    [Fact]
    public async Task Upload_SharesHeavy_ModelThrows_RejectsContext()
    {
        host.ChatClient.Respond = _ => throw new HttpRequestException("connection refused");

        var advisory = await UploadAndReadAsync("2026-04-30");

        AssertNoContext(advisory, "2026-04-30");
        Assert.Equal((0L, "Rejected", "ModelError"), await StoredStateAsync(advisory.SubmissionId));
    }

    [Fact]
    public async Task Upload_SharesHeavy_ModelTimesOut_RejectsContext()
    {
        host.ChatClient.Respond = async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Context;
        };

        var advisory = await UploadAndReadAsync("2026-03-31");

        AssertNoContext(advisory, "2026-03-31");
        Assert.Equal((0L, "Rejected", "Timeout"), await StoredStateAsync(advisory.SubmissionId));
    }

    [Fact]
    public async Task GetAdvisory_ContextOfOtherTenant_ReturnsNotFound()
    {
        host.ChatClient.Respond = _ => Task.FromResult(Context);
        var advisory = await UploadAndReadAsync("2026-02-28");
        using var bob = host.CreateClient(DevIdentities.Bob);

        using var response = await bob.GetAsync(PathFor(advisory.SubmissionId), _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAdvisory_UnknownNarrator_ReturnsNoContext()
    {
        // A row written by the first design (narrator = 'Model'): the API fails closed and drops the context.
        host.ChatClient.Respond = _ => Task.FromResult(Context);
        var advisory = await UploadAndReadAsync("2026-01-31");
        await using (var connection = new NpgsqlConnection(host.ConnectionString))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE advisory.advisories SET narrator = 'Model' WHERE submission_id = @SubmissionId",
                new { advisory.SubmissionId }, cancellationToken: _ct));
        }

        using var client = host.CreateClient(DevIdentities.Alice);
        var read = await client.GetFromJsonAsync<AdvisoryResponse>(PathFor(advisory.SubmissionId), _ct);

        Assert.Equal(("Model", (string?)null), (read!.Narrator, read.AiContext));
    }

    [Fact]
    public async Task Upload_Balanced_ProposalNotNeeded_RequestsNoContext()
    {
        // No proposal, nothing to explain (D-295): the model is never asked.
        host.ChatClient.Respond = _ => Task.FromResult(Context);
        var calls = host.ChatClient.Calls;

        var (session, advisory) = await UploadAndTrackAsync("2025-12-31", "valid-balanced.csv");

        Assert.Empty(session.Sent.MessagesOf<RefineNarrative>());
        Assert.Equal(calls, host.ChatClient.Calls);
        Assert.Equal(("Template", (string?)null, "NotRequested", (string?)null),
            (advisory.Narrator, advisory.Model, advisory.Refinement, advisory.AiContext));
        Assert.Equal((0L, "NotRequested", (string?)null), await StoredStateAsync(advisory.SubmissionId));
    }

    private static void AssertTemplate(AdvisoryResponse advisory, string cutOffDate)
    {
        Assert.Equal("Template", advisory.Narrator);
        var template = AdvisoryTestData.SharesHeavyNarrative.Replace("2026-06-30", cutOffDate, StringComparison.Ordinal);
        Assert.Equal(template.Split("\n\n"), advisory.Paragraphs);
    }

    private static void AssertNoContext(AdvisoryResponse advisory, string cutOffDate)
    {
        AssertTemplate(advisory, cutOffDate);
        Assert.Equal(((string?)null, "Rejected", (string?)null), (advisory.Model, advisory.Refinement, advisory.AiContext));
    }

    private async Task<AdvisoryResponse> UploadAndReadAsync(string cutOffDate) =>
        (await UploadAndTrackAsync(cutOffDate, "valid-shares-heavy.csv")).Advisory;

    // Upload as Alice (seeded on this host's database) and wait until the refinement chain has finished.
    private async Task<(ITrackedSession Session, AdvisoryResponse Advisory)> UploadAndTrackAsync(
        string cutOffDate, string sample)
    {
        using var client = host.CreateClient(DevIdentities.Alice);
        SubmissionReceivedResponse? body = null;
        var session = await host.Factory.Services.TrackActivity().Timeout(TimeSpan.FromSeconds(30))
            .DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync((Func<Wolverine.IMessageContext, Task>)(async _ =>
            {
                using var response = await client.UploadAsync(
                    "portfolio.csv", _ct, AdvisoryTestData.Sample(sample), cutOffDate);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                body = await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(_ct);
            }));

        var advisory = await client.GetFromJsonAsync<AdvisoryResponse>(PathFor(body!.SubmissionId), _ct);
        return (session, advisory!);
    }

    // Dead letters on this host's database, and the stored refinement columns.
    private async Task<(long DeadLetters, string Status, string? Reason)> StoredStateAsync(Guid submissionId)
    {
        await using var connection = new NpgsqlConnection(host.ConnectionString);
        var deadLetters = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM wolverine.wolverine_dead_letters", cancellationToken: _ct));
        var row = await connection.QuerySingleAsync<(string, string?)>(new CommandDefinition(
            "SELECT refinement_status, refinement_reason FROM advisory.advisories WHERE submission_id = @submissionId",
            new { submissionId }, cancellationToken: _ct));
        return (deadLetters, row.Item1, row.Item2);
    }

    private static string PathFor(Guid submissionId) => $"/api/advisory/submissions/{submissionId}/advisory";
}
