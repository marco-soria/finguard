using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FinGuard.Api.IntegrationTests.Portfolio;
using FinGuard.Compliance.Features.GetEvaluation;
using FinGuard.Tenancy.Development;
using Npgsql;

namespace FinGuard.Api.IntegrationTests.Compliance;

/// <summary>Brief #14 (D-197): <c>GET /api/v1/compliance/submissions/{id}/evaluation</c>, Dapper with the tenant filter.</summary>
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
            (submissionId, new DateOnly(2026, 6, 30), "Breached", 1, 3_000_000.00m, 0.90m),
            (evaluation.SubmissionId, evaluation.CutOffDate, evaluation.OverallStatus, evaluation.PolicyVersion,
             evaluation.TotalBookValue, evaluation.AlertThreshold));
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
    public async Task Upload_SharesHeavy_ApiReturnsTheStoredFullProposalInPresentationOrder()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        using var client = fixture.CreateClient(identity);
        var submissionId = await UploadAsync(client, "valid-shares-heavy.csv");

        var evaluation = await client.GetFromJsonAsync<EvaluationResponse>(PathFor(submissionId), _ct);

        // Same amounts as the stored rows (EvaluatePortfolioTests.Upload_SharesHeavy_StoresFullProposalPreservingTotal):
        // the reducer first, then receivers by amount received.
        Assert.NotNull(evaluation?.Proposal);
        Assert.Equal("Full", evaluation.Proposal.Status);
        Assert.Equal(
            [
                new MoveItem("Shares", 2_200_000.00m, 637_500.00m, -1_562_500.00m),
                new MoveItem("Cash", 300_000.00m, 993_374.43m, 693_374.43m),
                new MoveItem("RealEstate", 100_000.00m, 617_623.26m, 517_623.26m),
                new MoveItem("FixedIncome", 400_000.00m, 751_502.31m, 351_502.31m),
            ],
            evaluation.Proposal.Moves);
        Assert.Equal(0m, evaluation.Proposal.Moves.Sum(m => m.Change));
    }

    [Fact]
    public async Task GetEvaluation_Balanced_ReturnsNotNeededWithoutMoves()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice);
        var submissionId = await UploadAsync(client, Csv("350.00", "250.00", "200.00", "200.00"));

        var evaluation = await client.GetFromJsonAsync<EvaluationResponse>(PathFor(submissionId), _ct);

        Assert.Equal("NotNeeded", evaluation?.Proposal?.Status);
        Assert.Empty(evaluation!.Proposal!.Moves);
    }

    [Fact]
    public async Task GetEvaluation_LegacyWithoutProposal_ReturnsNullProposal()
    {
        // An evaluation stored before the rebalance engine: the two new columns were never written (D-236).
        var identity = await fixture.ProvisionTenantAsync(_ct);
        var submissionId = Guid.CreateVersion7();
        await InsertLegacyEvaluationAsync(identity.TenantId, submissionId);
        using var client = fixture.CreateClient(identity);

        using var response = await client.GetAsync(PathFor(submissionId), _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("proposal").ValueKind);
        Assert.Equal(4, json.RootElement.GetProperty("classes").GetArrayLength());
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
        new($"/api/v1/compliance/submissions/{submissionId}/evaluation", UriKind.Relative);

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

    private async Task InsertLegacyEvaluationAsync(Guid tenantId, Guid submissionId)
    {
        var evaluationId = Guid.CreateVersion7();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO compliance.evaluations
                (id, tenant_id, submission_id, cut_off_date, evaluated_at, total_book_value, overall_status,
                 policy_version, cash_limit, fixed_income_limit, shares_limit, real_estate_limit, alert_threshold,
                 rebalance_target_ratio)
            VALUES (@evaluationId, @tenantId, @submissionId, DATE '2026-06-30', now(), 100.00, 'Compliant',
                    1, 0.40, 0.30, 0.25, 0.25, 0.90, 0.85);
            INSERT INTO compliance.evaluation_classes (evaluation_id, tenant_id, asset_class, book_value, share, utilization, status)
            VALUES (@evaluationId, @tenantId, 'Cash', 35.00, 0.35, 0.875, 'Compliant'),
                   (@evaluationId, @tenantId, 'FixedIncome', 25.00, 0.25, 0.25 / 0.30, 'Compliant'),
                   (@evaluationId, @tenantId, 'Shares', 20.00, 0.20, 0.80, 'Compliant'),
                   (@evaluationId, @tenantId, 'RealEstate', 20.00, 0.20, 0.80, 'Compliant');
            """,
            new { evaluationId, tenantId, submissionId }, cancellationToken: _ct));
    }

    private async Task AssertNotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        Assert.Equal("Compliance.Evaluation.NotFound", problem.RootElement.GetProperty("code").GetString());
    }
}
