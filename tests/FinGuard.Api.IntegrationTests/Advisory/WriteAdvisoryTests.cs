using System.Net;
using Dapper;
using FinGuard.Api.IntegrationTests.Portfolio;
using FinGuard.Compliance.Contracts;
using FinGuard.Compliance.Contracts.Events;
using FinGuard.Portfolio.Features.ReceiveSubmission;
using FinGuard.Tenancy.Development;
using Npgsql;
using Wolverine;
using Wolverine.Tracking;

namespace FinGuard.Api.IntegrationTests.Advisory;

/// <summary>
/// Brief #18 (D-251, D-255): every <see cref="EvaluationCompleted"/> writes one template advisory, idempotent by
/// <c>EventId</c>. Runs on the shared Api host; assertions match their own ids (D-165).
/// </summary>
[Collection(SubmissionEvents.Collection)]
public sealed class WriteAdvisoryTests(ApiFixture fixture)
{
    private static readonly Guid Acme = DevIdentities.Alice.TenantId;

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_SharesHeavy_WritesTemplateAdvisory()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);

        var (_, response) = await UploadAsync(identity, AdvisoryTestData.Sample("valid-shares-heavy.csv"), "2026-06-30");

        var row = Assert.Single(await AdvisoryRowsAsync(response.SubmissionId));
        Assert.Equal(
            (identity.TenantId, "Template", AdvisoryTestData.SharesHeavyNarrative),
            (row.TenantId, row.Narrator, row.Narrative));
        Assert.Contains("\"proposalStatus\": \"Full\"", row.Facts, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Upload_Valid_RoutesEvaluationCompletedToAdvisory()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);

        var (session, response) = await UploadAsync(identity, AdvisoryTestData.Sample("valid-balanced.csv"), "2026-06-30");

        // D-199 / R-40: the event had no route before Advisory subscribed.
        var records = session.AllRecordsInOrder()
            .Where(r => r.Message is EvaluationCompleted e && e.SubmissionId == response.SubmissionId)
            .Select(r => r.MessageEventType)
            .ToList();
        Assert.Contains(MessageEventType.Sent, records);
        Assert.DoesNotContain(MessageEventType.NoRoutes, records);
        Assert.Contains(MessageEventType.MessageSucceeded, records);
    }

    [Fact]
    public async Task EvaluationCompleted_Duplicate_WritesOneAdvisory()
    {
        var @event = Event(Acme);

        await DeliverTrackedAsync(Acme, @event);
        await DeliverTrackedAsync(Acme, @event);

        var row = Assert.Single(await AdvisoryRowsAsync(@event.SubmissionId));
        Assert.Equal((Acme, @event.EvaluationId), (row.TenantId, row.EvaluationId));
    }

    [Fact]
    public async Task AdvisoryFlow_Always_LogsIdsOnly()
    {
        var @event = Event(Acme);

        await DeliverTrackedAsync(Acme, @event);

        var records = fixture.Logs.GetSnapshot();
        Assert.Contains(records, r => r.Message == $"Wrote advisory for evaluation {@event.EvaluationId}");
        var leaks = records
            .Where(r => r.Message.Contains("987,654", StringComparison.Ordinal)
                        || r.Message.Contains("987654", StringComparison.Ordinal)
                        || r.Message.Contains("investment advice", StringComparison.Ordinal))
            .Select(r => r.Message)
            .ToList();
        Assert.True(leaks.Count == 0, $"Log records leak facts or narrative: {string.Join(" | ", leaks)}");
    }

    internal static EvaluationCompleted Event(Guid tenantId)
    {
        // Shares 987,654.32 of 987,654.32: Breached; the proposal moves the surplus to the other classes.
        var now = DateTimeOffset.UtcNow;
        return new EvaluationCompleted(
            Guid.CreateVersion7(now), tenantId, now, Guid.CreateVersion7(now), Guid.CreateVersion7(now),
            new DateOnly(2026, 1, 31), ComplianceStatusCode.Breached, 1,
            [
                new(AssetClassCode.Cash, 0m, 0.40m, 0m, ComplianceStatusCode.Compliant, 0m),
                new(AssetClassCode.FixedIncome, 0m, 0.30m, 0m, ComplianceStatusCode.Compliant, 0m),
                new(AssetClassCode.Shares, 987_654.32m, 0.25m, 4m, ComplianceStatusCode.Breached, 987_654.32m),
                new(AssetClassCode.RealEstate, 0m, 0.25m, 0m, ComplianceStatusCode.Compliant, 0m),
            ],
            ProposalStatusCode.Partial, 0.90m, 0.85m);
    }

    private async Task<(ITrackedSession Session, SubmissionReceivedResponse Response)> UploadAsync(
        DevIdentity identity, byte[] content, string cutOffDate)
    {
        using var client = fixture.CreateClient(identity);
        return await SubmissionEvents.UploadTrackedAsync(fixture, client, content, cutOffDate, HttpStatusCode.Created, _ct);
    }

    // Through the durable local queue, the way a cascaded event arrives.
    private Task<ITrackedSession> DeliverTrackedAsync(Guid tenantId, EvaluationCompleted @event) =>
        fixture.Factory.Services.TrackActivity().DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync(context =>
                context.PublishAsync(@event, new DeliveryOptions { TenantId = tenantId.ToString() }).AsTask());

    // Raw SQL as the database owner: sees every row, whatever the query filter.
    private async Task<List<AdvisoryDbRow>> AdvisoryRowsAsync(Guid submissionId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        var rows = await connection.QueryAsync<AdvisoryDbRow>(new CommandDefinition(
            """
            SELECT tenant_id AS TenantId, evaluation_id AS EvaluationId, narrator AS Narrator, narrative AS Narrative,
                   facts::text AS Facts
            FROM advisory.advisories WHERE submission_id = @submissionId
            """,
            new { submissionId }, cancellationToken: _ct));
        return rows.ToList();
    }

    private sealed record AdvisoryDbRow(Guid TenantId, Guid EvaluationId, string Narrator, string Narrative, string Facts);
}
