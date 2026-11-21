using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinGuard.Api.IntegrationTests.Portfolio;
using FinGuard.Compliance.Features.GetEvaluation;
using FinGuard.Tenancy.Development;

namespace FinGuard.Api.IntegrationTests.Compliance;

/// <summary>Brief #14 (D-197): <c>GET /api/compliance/submissions/{id}/evaluation</c>, Dapper with the tenant filter.</summary>
[Collection(SubmissionEvents.Collection)]
public sealed class GetEvaluationTests(ApiFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetEvaluation_Own_Returns200WithClassesAndAlerts()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        using var client = fixture.CreateClient(identity);
        var submissionId = await UploadAsync(client, "valid-shares-heavy.csv");

        var evaluation = await client.GetFromJsonAsync<EvaluationResponse>(PathFor(submissionId), _ct);

        Assert.NotNull(evaluation);
        Assert.Equal(
            (submissionId, new DateOnly(2026, 6, 30), "Breached", 1, 3_000_000.00m),
            (evaluation.SubmissionId, evaluation.CutOffDate, evaluation.OverallStatus, evaluation.PolicyVersion,
             evaluation.TotalBookValue));
        // Exact decimals: the API never rounds (D-197). 2.2 / 3 keeps every digit decimal has.
        var sharesShare = 2_200_000.00m / 3_000_000.00m;
        Assert.Equal(
            [
                new ClassEvaluationItem("Cash", 300_000.00m, 0.1m, 0.40m, 0.1m / 0.40m, "Compliant"),
                new ClassEvaluationItem(
                    "FixedIncome", 400_000.00m, 400_000.00m / 3_000_000.00m, 0.30m,
                    400_000.00m / 3_000_000.00m / 0.30m, "Compliant"),
                new ClassEvaluationItem("Shares", 2_200_000.00m, sharesShare, 0.25m, sharesShare / 0.25m, "Breached"),
                new ClassEvaluationItem(
                    "RealEstate", 100_000.00m, 100_000.00m / 3_000_000.00m, 0.25m,
                    100_000.00m / 3_000_000.00m / 0.25m, "Compliant"),
            ],
            evaluation.Classes);
        Assert.Equal([new AlertItem("Shares", "Breached", sharesShare / 0.25m)], evaluation.Alerts);
    }

    [Fact]
    public async Task GetEvaluation_ExactlyAtLimit_ReturnsWarningWithUtilizationOne()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice);
        var submissionId = await UploadAsync(client, Csv("350.00", "250.00", "250.00", "150.00"));

        var evaluation = await client.GetFromJsonAsync<EvaluationResponse>(PathFor(submissionId), _ct);

        Assert.NotNull(evaluation);
        Assert.Equal("Warning", evaluation.OverallStatus);
        Assert.Equal([new AlertItem("Shares", "Warning", 1m)], evaluation.Alerts);
    }

    [Fact]
    public async Task GetEvaluation_OtherTenants_Returns404()
    {
        using var bob = fixture.CreateClient(DevIdentities.Bob);
        var bobsSubmission = await UploadAsync(bob, Csv("10.00", "0", "5.00", "0"));
        using var alice = fixture.CreateClient(DevIdentities.Alice);

        using var response = await alice.GetAsync(PathFor(bobsSubmission), _ct);
        using var own = await bob.GetAsync(PathFor(bobsSubmission), _ct);

        await AssertNotFoundAsync(response);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task GetEvaluation_UnknownSubmission_Returns404()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice);

        using var response = await client.GetAsync(PathFor(Guid.NewGuid()), _ct);

        await AssertNotFoundAsync(response);
    }

    private static Uri PathFor(Guid submissionId) =>
        new($"/api/compliance/submissions/{submissionId}/evaluation", UriKind.Relative);

    private static byte[] Csv(string cash, string fixedIncome, string shares, string realEstate) =>
        System.Text.Encoding.UTF8.GetBytes(
            "position_id,asset_class,book_value,currency,description\n"
            + $"{SubmissionTestData.UniquePositionId()},cash,{cash},USD,x\n"
            + $"P-2,fixed_income,{fixedIncome},USD,x\nP-3,shares,{shares},USD,x\nP-4,real_estate,{realEstate},USD,x\n");

    private async Task<Guid> UploadAsync(HttpClient client, string sample) =>
        await UploadAsync(client, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", sample)));

    // Tracked, so the evaluation is stored before the GET.
    private async Task<Guid> UploadAsync(HttpClient client, byte[] content)
    {
        var (_, response) = await SubmissionEvents.UploadTrackedAsync(
            fixture, client, content, "2026-06-30", HttpStatusCode.Created, _ct);
        return response.SubmissionId;
    }

    private async Task AssertNotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        Assert.Equal("Compliance.Evaluation.NotFound", problem.RootElement.GetProperty("code").GetString());
    }
}
