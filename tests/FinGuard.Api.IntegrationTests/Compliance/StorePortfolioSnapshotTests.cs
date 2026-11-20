using System.Net;
using System.Text;
using Dapper;
using FinGuard.Api.IntegrationTests.Portfolio;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Compliance.Persistence;
using FinGuard.Portfolio.Contracts;
using FinGuard.Portfolio.Contracts.Events;
using FinGuard.Tenancy.Development;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wolverine;
using Wolverine.Tracking;

namespace FinGuard.Api.IntegrationTests.Compliance;

/// <summary>
/// Brief #12 (D-157): Compliance stores a snapshot per <see cref="SubmissionAccepted"/>, idempotent and tenant-owned.
/// Runs on the real Api host and module registration (R-24). Assertions match their own ids: the host is shared.
/// </summary>
[Collection(SubmissionEvents.Collection)]
public sealed class StorePortfolioSnapshotTests(ApiFixture fixture)
{
    private const string MarkerAmount = "987654.32";
    private static readonly Guid Acme = DevIdentities.Alice.TenantId;
    private static readonly Guid Globex = DevIdentities.Bob.TenantId;

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_ValidCsv_ComplianceStoresSnapshotForUploadersTenant()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice);
        var content = Encoding.UTF8.GetBytes(
            "position_id,asset_class,book_value,currency,description\n"
            + $"{SubmissionTestData.UniquePositionId()},cash,400.10,USD,a\n"
            + "P-2,fixed_income,300.20,USD,b\n"
            + "P-3,shares,200.30,USD,c\n"
            + "P-4,real_estate,100.40,USD,d\n");

        var (session, response) = await SubmissionEvents.UploadTrackedAsync(
            fixture, client, content, "2026-02-28", HttpStatusCode.Created, _ct);

        var consumed = Assert.Single(SubmissionEvents.Consumed(session, response.SubmissionId));
        var row = Assert.Single(await SnapshotRowsAsync(response.SubmissionId));
        // PostgreSQL keeps microseconds; a .NET tick is 100 ns.
        var receivedAt = response.ReceivedAt.AddTicks(-(response.ReceivedAt.Ticks % 10));
        Assert.Equal(
            (Acme, new DateOnly(2026, 2, 28), receivedAt, consumed.EventId),
            (row.TenantId, row.CutOffDate, new DateTimeOffset(row.ReceivedAt), row.SourceEventId));
        Assert.Equal((400.10m, 300.20m, 200.30m, 100.40m), (row.Cash, row.FixedIncome, row.Shares, row.RealEstate));
    }

    [Fact]
    public async Task SubmissionAccepted_DeliveredTwice_StoresOneSnapshot()
    {
        var @event = Event(Acme);

        await InvokeForTenantAsync(Acme, @event);
        await InvokeForTenantAsync(Acme, @event);

        Assert.Single(await SnapshotRowsAsync(@event.SubmissionId));
    }

    [Fact]
    public async Task SubmissionAccepted_PayloadTenantDiffersFromEnvelope_IsRejected()
    {
        var @event = Event(Globex);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeForTenantAsync(Acme, @event));

        Assert.Equal("The message tenant id differs from the envelope tenant id.", exception.Message);
        Assert.Empty(await SnapshotRowsAsync(@event.SubmissionId));
    }

    [Fact]
    public async Task SubmissionAccepted_ForBob_IsInvisibleToAlicesComplianceContext()
    {
        var @event = Event(Globex);
        await InvokeForTenantAsync(Globex, @event);

        var aliceSees = await VisibleSnapshotCountAsync(Acme, @event.SubmissionId);
        var bobSees = await VisibleSnapshotCountAsync(Globex, @event.SubmissionId);

        Assert.Equal((0, 1), (aliceSees, bobSees));
    }

    [Fact]
    public async Task SubmissionAccepted_InvalidTotals_GoesToErrorQueueAndStoresNothing()
    {
        var @event = Event(Acme) with { Totals = [new AssetClassTotal(AssetClassCode.Cash, 1m)] };

        // Through the durable local queue, the way a cascaded event arrives.
        var session = await fixture.Factory.Services.TrackActivity().DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync(context =>
                context.PublishAsync(@event, new DeliveryOptions { TenantId = Acme.ToString() }).AsTask());

        var failure = Assert.Single(session.AllRecordsInOrder(), r =>
            r.Message is SubmissionAccepted e && e.EventId == @event.EventId
            && r.MessageEventType == MessageEventType.MovedToErrorQueue);
        Assert.IsType<InvalidOperationException>(failure.Exception);
        Assert.Empty(await SnapshotRowsAsync(@event.SubmissionId));
    }

    [Fact]
    public async Task UploadAndSnapshot_Always_LogNoAmounts()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice);
        var content = Encoding.UTF8.GetBytes(
            "position_id,asset_class,book_value,currency,description\n"
            + $"{SubmissionTestData.UniquePositionId()},shares,{MarkerAmount},USD,a\n");

        var (session, response) = await SubmissionEvents.UploadTrackedAsync(
            fixture, client, content, SubmissionTestData.CutOffDate, HttpStatusCode.Created, _ct);

        Assert.Single(SubmissionEvents.Consumed(session, response.SubmissionId));
        var records = fixture.Logs.GetSnapshot();
        Assert.Contains(records, r =>
            r.Message == $"Stored portfolio snapshot for submission {response.SubmissionId}");
        // The integer part catches any culture's decimal separator.
        var leaks = records
            .Where(r => r.Message.Contains("987654", StringComparison.Ordinal)
                        || r.StructuredState?.Any(kv => kv.Value?.Contains("987654", StringComparison.Ordinal) == true) == true
                        || r.Exception?.ToString().Contains("987654", StringComparison.Ordinal) == true)
            .Select(r => $"[{r.Level}] {r.Category}: {r.Message}")
            .ToList();
        Assert.True(leaks.Count == 0, $"Log records leak an amount: {string.Join(" | ", leaks)}");
    }

    private static SubmissionAccepted Event(Guid tenantId)
    {
        var now = DateTimeOffset.UtcNow;
        return new SubmissionAccepted(
            Guid.CreateVersion7(now), tenantId, now, Guid.CreateVersion7(now), new DateOnly(2026, 1, 31), now,
            [
                new AssetClassTotal(AssetClassCode.Cash, 10m), new AssetClassTotal(AssetClassCode.FixedIncome, 0m),
                new AssetClassTotal(AssetClassCode.Shares, 5m), new AssetClassTotal(AssetClassCode.RealEstate, 0m),
            ]);
    }

    private async Task InvokeForTenantAsync(Guid tenantId, object message)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeForTenantAsync(tenantId.ToString(), message, _ct);
    }

    // Raw SQL as the database owner: sees every row, whatever the query filter.
    private async Task<List<SnapshotRow>> SnapshotRowsAsync(Guid submissionId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        var rows = await connection.QueryAsync<SnapshotRow>(new CommandDefinition(
            """
            SELECT tenant_id AS TenantId, cut_off_date AS CutOffDate, received_at AS ReceivedAt, cash AS Cash,
                   fixed_income AS FixedIncome, shares AS Shares, real_estate AS RealEstate,
                   source_event_id AS SourceEventId
            FROM compliance.portfolio_snapshots WHERE submission_id = @submissionId
            """,
            new { submissionId }, cancellationToken: _ct));
        return rows.ToList();
    }

    // Through the Compliance context, so the tenant query filter decides what is visible.
    private async Task<int> VisibleSnapshotCountAsync(Guid tenantId, Guid submissionId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<ComplianceDbContext>();
        options.UseModuleNpgsql(fixture.ConnectionString, ComplianceDbContext.Schema)
            .AddInterceptors(TenantStampingInterceptor.Instance);
        await using var db = new ComplianceDbContext(options.Options, tenantContext);
        return await db.Set<PortfolioSnapshot>().CountAsync(s => s.SubmissionId == submissionId, _ct);
    }

    // Npgsql hands timestamptz to Dapper as a UTC DateTime and date as DateOnly.
    private sealed record SnapshotRow(
        Guid TenantId, DateOnly CutOffDate, DateTime ReceivedAt, decimal Cash, decimal FixedIncome, decimal Shares,
        decimal RealEstate, Guid SourceEventId);
}
