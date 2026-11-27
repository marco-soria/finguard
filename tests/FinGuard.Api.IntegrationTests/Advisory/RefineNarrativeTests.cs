using System.Net;
using System.Net.Http.Json;
using Dapper;
using FinGuard.Advisory.Features.GetAdvisory;
using FinGuard.Api.IntegrationTests.Portfolio;
using FinGuard.Portfolio.Features.ReceiveSubmission;
using FinGuard.Tenancy.Development;
using Npgsql;
using Wolverine.Tracking;

namespace FinGuard.Api.IntegrationTests.Advisory;

/// <summary>
/// Brief #19 (D-281, D-282): after the template is stored, the model's text replaces it only when it passes
/// validation; every expected failure keeps the template, records a reason and leaves no dead letter.
/// </summary>
public sealed class RefineNarrativeTests(ModelNarratorHost host) : IClassFixture<ModelNarratorHost>
{
    // The shares-heavy sample in different words; every number copied from the facts, no disclaimer.
    private const string Paraphrase =
        "On 2026-06-30, the portfolio breaches its limit for Shares under policy version 1. Shares make up 73.33 % " +
        "of the portfolio while the limit is 25.00 %, so 293.33 % of the limit is used.\n\n" +
        "The proposal reduces Shares by $1,562,500.00 to $637,500.00, which is 85.00 % of its limit. The released " +
        "amount goes to Cash (+$693,374.43), Real estate (+$517,623.26) and Fixed income (+$351,502.31). After " +
        "these moves, every asset class is within its limit.";

    private const string Disclaimer = "This text explains a deterministic calculation. It is not investment advice.";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_SharesHeavy_ModelTextAccepted_StoresModelNarrative()
    {
        host.ChatClient.Respond = _ => Task.FromResult(Paraphrase);

        var advisory = await UploadAndReadAsync("2026-06-30");

        Assert.Equal(("Model", "fake-model", "Accepted"), (advisory.Narrator, advisory.Model, advisory.Refinement));
        Assert.Equal([.. Paraphrase.Split("\n\n"), Disclaimer], advisory.Paragraphs);
        Assert.Equal((0L, "Accepted", (string?)null), await StoredStateAsync(advisory.SubmissionId));
    }

    [Fact]
    public async Task Upload_SharesHeavy_ModelChangesANumber_KeepsTemplate()
    {
        host.ChatClient.Respond = _ => Task.FromResult(Paraphrase.Replace("$637,500.00", "$637,600.00", StringComparison.Ordinal));

        var advisory = await UploadAndReadAsync("2026-05-31");

        AssertTemplateKept(advisory, "2026-05-31");
        Assert.Equal((0L, "Rejected", "UnknownNumber"), await StoredStateAsync(advisory.SubmissionId));
    }

    [Fact]
    public async Task Upload_SharesHeavy_ModelThrows_KeepsTemplate()
    {
        host.ChatClient.Respond = _ => throw new HttpRequestException("connection refused");

        var advisory = await UploadAndReadAsync("2026-04-30");

        AssertTemplateKept(advisory, "2026-04-30");
        Assert.Equal((0L, "Rejected", "ModelError"), await StoredStateAsync(advisory.SubmissionId));
    }

    [Fact]
    public async Task Upload_SharesHeavy_ModelTimesOut_KeepsTemplate()
    {
        host.ChatClient.Respond = async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Paraphrase;
        };

        var advisory = await UploadAndReadAsync("2026-03-31");

        AssertTemplateKept(advisory, "2026-03-31");
        Assert.Equal((0L, "Rejected", "Timeout"), await StoredStateAsync(advisory.SubmissionId));
    }

    [Fact]
    public async Task GetAdvisory_ModelNarrativeOfOtherTenant_ReturnsNotFound()
    {
        host.ChatClient.Respond = _ => Task.FromResult(Paraphrase);
        var advisory = await UploadAndReadAsync("2026-02-28");
        using var bob = host.CreateClient(DevIdentities.Bob);

        using var response = await bob.GetAsync(PathFor(advisory.SubmissionId), _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static void AssertTemplateKept(AdvisoryResponse advisory, string cutOffDate)
    {
        Assert.Equal(("Template", (string?)null, "Rejected"), (advisory.Narrator, advisory.Model, advisory.Refinement));
        var template = AdvisoryTestData.SharesHeavyNarrative.Replace("2026-06-30", cutOffDate, StringComparison.Ordinal);
        Assert.Equal(template.Split("\n\n"), advisory.Paragraphs);
    }

    // Upload as Alice (seeded on this host's database) and wait until the refinement chain has finished.
    private async Task<AdvisoryResponse> UploadAndReadAsync(string cutOffDate)
    {
        using var client = host.CreateClient(DevIdentities.Alice);
        SubmissionReceivedResponse? body = null;
        await host.Factory.Services.TrackActivity().Timeout(TimeSpan.FromSeconds(30)).DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync((Func<Wolverine.IMessageContext, Task>)(async _ =>
            {
                using var response = await client.UploadAsync(
                    "portfolio.csv", _ct, AdvisoryTestData.Sample("valid-shares-heavy.csv"), cutOffDate);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                body = await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(_ct);
            }));

        var advisory = await client.GetFromJsonAsync<AdvisoryResponse>(PathFor(body!.SubmissionId), _ct);
        return advisory!;
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
