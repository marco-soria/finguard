using System.Net;
using System.Text;
using Dapper;
using FinGuard.Api.IntegrationTests.Portfolio;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Compliance.Contracts;
using FinGuard.Compliance.Contracts.Events;
using FinGuard.Compliance.Features.EvaluatePortfolio;
using FinGuard.Compliance.Persistence;
using FinGuard.Portfolio.Contracts.Events;
using FinGuard.Portfolio.Features.ReceiveSubmission;
using FinGuard.Tenancy.Development;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wolverine;
using Wolverine.Tracking;
using PortfolioAssetClass = FinGuard.Portfolio.Contracts.AssetClassCode;

namespace FinGuard.Api.IntegrationTests.Compliance;

/// <summary>
/// Brief #14 (D-191 to D-195): every accepted submission is evaluated once, stored with its policy snapshot, and
/// cascades one <see cref="EvaluationCompleted"/>. Runs on the shared Api host; assertions match their own ids (D-165).
/// </summary>
[Collection(SubmissionEvents.Collection)]
public sealed class EvaluatePortfolioTests(ApiFixture fixture)
{
    private const string Header = "position_id,asset_class,book_value,currency,description\n";
    private static readonly Guid Acme = DevIdentities.Alice.TenantId;
    private static readonly Guid Globex = DevIdentities.Bob.TenantId;

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_ValidBalanced_IsEvaluatedCompliantWithPolicyVersionOne()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);

        var (_, response) = await UploadAsync(identity, Sample("valid-balanced.csv"), "2026-06-30");

        var evaluation = Assert.Single(await EvaluationRowsAsync(response.SubmissionId));
        Assert.Equal(
            (identity.TenantId, new DateOnly(2026, 6, 30), "Compliant", 5_000_000.00m),
            (evaluation.TenantId, evaluation.CutOffDate, evaluation.OverallStatus, evaluation.TotalBookValue));
        Assert.Equal(
            (1, 0.40m, 0.30m, 0.25m, 0.25m, 0.90m, 0.85m),
            (evaluation.PolicyVersion, evaluation.CashLimit, evaluation.FixedIncomeLimit, evaluation.SharesLimit,
             evaluation.RealEstateLimit, evaluation.AlertThreshold, evaluation.RebalanceTargetRatio));
        var classes = await ClassRowsAsync(evaluation.Id);
        Assert.Equal(["Cash", "FixedIncome", "Shares", "RealEstate"], classes.Select(c => c.AssetClass));
        Assert.All(classes, c => Assert.Equal(("Compliant", identity.TenantId), (c.Status, c.TenantId)));
    }

    [Fact]
    public async Task Upload_SharesHeavy_IsEvaluatedBreachedWithSharesAlert()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);

        var (_, response) = await UploadAsync(identity, Sample("valid-shares-heavy.csv"), "2026-06-30");

        var evaluation = Assert.Single(await EvaluationRowsAsync(response.SubmissionId));
        Assert.Equal("Breached", evaluation.OverallStatus);
        var alerts = (await ClassRowsAsync(evaluation.Id)).Where(c => c.Status != "Compliant");
        Assert.Equal([("Shares", "Breached")], alerts.Select(c => (c.AssetClass, c.Status)));
    }

    [Fact]
    public async Task Upload_ExactlyAtLimit_StoresUtilizationOneAndWarning()
    {
        // Shares 0.25 of the total against a 0.25 limit: u = 1 exactly. Cash 0.875, fixed income 0.8333, real estate 0.6.
        var content = Csv(("cash", "350.00"), ("fixed_income", "250.00"), ("shares", "250.00"), ("real_estate", "150.00"));

        var (_, response) = await UploadAsync(DevIdentities.Alice, content, SubmissionTestData.CutOffDate);

        var evaluation = Assert.Single(await EvaluationRowsAsync(response.SubmissionId));
        var shares = Assert.Single(await ClassRowsAsync(evaluation.Id), c => c.AssetClass == "Shares");
        Assert.Equal((1m, "Warning"), (shares.Utilization, shares.Status));
        Assert.Equal("Warning", evaluation.OverallStatus);
    }

    [Fact]
    public async Task Upload_JustAboveLimit_IsBreached()
    {
        // Shares 0.250025 of the total against a 0.25 limit: u = 1.0001.
        var content = Csv(
            ("cash", "350000.00"), ("fixed_income", "249975.00"), ("shares", "250025.00"), ("real_estate", "150000.00"));

        var (_, response) = await UploadAsync(DevIdentities.Alice, content, SubmissionTestData.CutOffDate);

        var evaluation = Assert.Single(await EvaluationRowsAsync(response.SubmissionId));
        var shares = Assert.Single(await ClassRowsAsync(evaluation.Id), c => c.AssetClass == "Shares");
        // Unconstrained numeric keeps the unrounded value (D-195).
        Assert.Equal((0.250025m, 1.0001m, "Breached"), (shares.Share, shares.Utilization, shares.Status));
        Assert.Equal("Breached", evaluation.OverallStatus);
    }

    [Fact]
    public async Task Upload_Valid_PublishesOneEvaluationCompleted()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);

        var (session, response) = await UploadAsync(identity, Sample("valid-shares-heavy.csv"), "2026-06-30");

        var published = Assert.Single(Cascaded(session, response.SubmissionId));
        var evaluation = Assert.Single(await EvaluationRowsAsync(response.SubmissionId));
        Assert.Equal(
            (identity.TenantId, evaluation.Id, new DateOnly(2026, 6, 30), ComplianceStatusCode.Breached, 1),
            (published.TenantId, published.EvaluationId, published.CutOffDate, published.OverallStatus,
             published.PolicyVersion));
        Assert.Equal(
            [
                (AssetClassCode.Cash, 300_000.00m, 0.40m, ComplianceStatusCode.Compliant),
                (AssetClassCode.FixedIncome, 400_000.00m, 0.30m, ComplianceStatusCode.Compliant),
                (AssetClassCode.Shares, 2_200_000.00m, 0.25m, ComplianceStatusCode.Breached),
                (AssetClassCode.RealEstate, 100_000.00m, 0.25m, ComplianceStatusCode.Compliant),
            ],
            published.Classes.Select(c => (c.AssetClass, c.BookValue, c.Limit, c.Status)));
    }

    [Fact]
    public async Task Upload_SharesHeavy_StoresFullProposalPreservingTotal()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);

        var (_, response) = await UploadAsync(identity, Sample("valid-shares-heavy.csv"), "2026-06-30");

        // Hand calculation: RebalanceEngineTests.Propose_SharesHeavySample_ReducesSharesToTargetAndSplitsSurplusByHeadroom.
        var evaluation = Assert.Single(await EvaluationRowsAsync(response.SubmissionId));
        Assert.Equal("Full", evaluation.ProposalStatus);
        var classes = await ClassRowsAsync(evaluation.Id);
        Assert.Equal(
            [
                ("Cash", 300_000.00m, (decimal?)993_374.43m),
                ("FixedIncome", 400_000.00m, (decimal?)751_502.31m),
                ("Shares", 2_200_000.00m, (decimal?)637_500.00m),
                ("RealEstate", 100_000.00m, (decimal?)617_623.26m),
            ],
            classes.Select(c => (c.AssetClass, c.BookValue, c.ProposedBookValue)));
        Assert.Equal(evaluation.TotalBookValue, classes.Sum(c => c.ProposedBookValue));
    }

    [Fact]
    public async Task Upload_ValidBalanced_StoresNotNeeded()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);

        var (_, response) = await UploadAsync(identity, Sample("valid-balanced.csv"), "2026-06-30");

        var evaluation = Assert.Single(await EvaluationRowsAsync(response.SubmissionId));
        Assert.Equal("NotNeeded", evaluation.ProposalStatus);
        Assert.All(await ClassRowsAsync(evaluation.Id), c => Assert.Equal(c.BookValue, c.ProposedBookValue));
    }

    [Fact]
    public async Task Upload_Valid_EvaluationCompletedCarriesProposal()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);

        var (session, response) = await UploadAsync(identity, Sample("valid-shares-heavy.csv"), "2026-06-30");

        var published = Assert.Single(Cascaded(session, response.SubmissionId));
        Assert.Equal(ProposalStatusCode.Full, published.ProposalStatus);
        Assert.Equal(
            [
                (AssetClassCode.Cash, 993_374.43m),
                (AssetClassCode.FixedIncome, 751_502.31m),
                (AssetClassCode.Shares, 637_500.00m),
                (AssetClassCode.RealEstate, 617_623.26m),
            ],
            published.Classes.Select(c => (c.AssetClass, c.ProposedBookValue)));
    }

    [Fact]
    public async Task EvaluatePortfolio_DeliveredTwice_StoresOneEvaluation()
    {
        var @event = Event(Acme, new DateOnly(2026, 1, 31));
        await DeliverTrackedAsync(Acme, @event);

        await InvokeForTenantAsync(Acme, new EvaluatePortfolio(@event.SubmissionId));
        await InvokeForTenantAsync(Acme, new EvaluatePortfolio(@event.SubmissionId));

        Assert.Single(await EvaluationRowsAsync(@event.SubmissionId));
    }

    [Fact]
    public async Task SubmissionAccepted_DeliveredTwice_EvaluatesOnce()
    {
        var @event = Event(Acme, new DateOnly(2026, 1, 31));

        var first = await DeliverTrackedAsync(Acme, @event);
        var second = await DeliverTrackedAsync(Acme, @event);

        Assert.Single(EvaluateCommands(first, @event.SubmissionId));
        Assert.Empty(EvaluateCommands(second, @event.SubmissionId));
        Assert.Single(await EvaluationRowsAsync(@event.SubmissionId));
    }

    [Fact]
    public async Task Upload_CutOffBefore2000_Returns400TooEarlyAndStoresNothing()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        using var client = fixture.CreateClient(identity);

        var session = await fixture.Factory.Services.TrackActivity().DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ =>
            {
                using var response = await client.UploadAsync("portfolio.csv", _ct, Sample("valid-balanced.csv"), "1999-12-31");
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("Portfolio.Submission.CutOffDateTooEarly", await response.Content.ReadAsStringAsync(_ct),
                    StringComparison.Ordinal);
            }));

        Assert.DoesNotContain(session.Sent.MessagesOf<SubmissionAccepted>(), e => e.TenantId == identity.TenantId);
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        var stored = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT (SELECT count(*) FROM portfolio.submissions WHERE tenant_id = @tenantId)
                 + (SELECT count(*) FROM compliance.evaluations WHERE tenant_id = @tenantId)
            """,
            new { tenantId = identity.TenantId }, cancellationToken: _ct));
        Assert.Equal(0, stored);
    }

    [Fact]
    public async Task Upload_CutOffOn2000_01_01_IsEvaluated()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);

        var (_, response) = await UploadAsync(identity, Sample("valid-balanced.csv"), "2000-01-01");

        var evaluation = Assert.Single(await EvaluationRowsAsync(response.SubmissionId));
        Assert.Equal((new DateOnly(2000, 1, 1), 1), (evaluation.CutOffDate, evaluation.PolicyVersion));
    }

    [Fact]
    public async Task EvaluatePortfolio_NoPolicyForCutOffDate_GoesToErrorQueueAndKeepsSnapshot()
    {
        // Only old data or a bug can reach Compliance with such a date after D-190.
        var @event = Event(Acme, new DateOnly(1999, 12, 31));

        var session = await DeliverTrackedAsync(Acme, @event);

        var failure = Assert.Single(session.AllRecordsInOrder(), r =>
            r.Message is EvaluatePortfolio c && c.SubmissionId == @event.SubmissionId
            && r.MessageEventType == MessageEventType.MovedToErrorQueue);
        var exception = Assert.IsType<InvalidOperationException>(failure.Exception);
        Assert.DoesNotContain("1999", exception.Message, StringComparison.Ordinal);
        Assert.Single(await SnapshotIdsAsync(@event.SubmissionId));
        Assert.Empty(await EvaluationRowsAsync(@event.SubmissionId));
    }

    [Fact]
    public async Task EvaluationFlow_Always_LogsIdsOnly()
    {
        var content = Csv(("shares", "987654.32"));

        var (_, response) = await UploadAsync(DevIdentities.Alice, content, "2003-04-05");

        var evaluation = Assert.Single(await EvaluationRowsAsync(response.SubmissionId));
        var records = fixture.Logs.GetSnapshot();
        Assert.Contains(records, r =>
            r.Message == $"Evaluated submission {response.SubmissionId} as {evaluation.Id}");
        // The integer part catches any culture's decimal separator. Statuses are checked on this flow's records only:
        // other tests share the log collector.
        var ids = new[] { response.SubmissionId.ToString(), evaluation.Id.ToString() };
        var leaks = records
            .Where(r => Text(r).Any(t => t.Contains("987654", StringComparison.Ordinal)
                                         || t.Contains("2003-04-05", StringComparison.Ordinal)
                                         || t.Contains("05/04/2003", StringComparison.Ordinal)
                                         || t.Contains("4/5/2003", StringComparison.Ordinal))
                        || (Text(r).Any(t => ids.Any(id => t.Contains(id, StringComparison.Ordinal)))
                            && Text(r).Any(t => t.Contains("Breached", StringComparison.Ordinal))))
            .Select(r => $"[{r.Level}] {r.Category}: {r.Message}")
            .ToList();
        Assert.True(leaks.Count == 0, $"Log records leak an amount, a date or a status: {string.Join(" | ", leaks)}");
    }

    [Fact]
    public async Task Evaluation_ForBob_IsInvisibleToAlicesComplianceContext()
    {
        var @event = Event(Globex, new DateOnly(2026, 1, 31));
        await DeliverTrackedAsync(Globex, @event);
        var evaluationId = Assert.Single(await EvaluationRowsAsync(@event.SubmissionId)).Id;

        var aliceSees = await VisibleRowCountsAsync(Acme, evaluationId);
        var bobSees = await VisibleRowCountsAsync(Globex, evaluationId);

        Assert.Equal(((0, 0), (1, 4)), (aliceSees, bobSees));
    }

    private static IEnumerable<string> Text(Microsoft.Extensions.Logging.Testing.FakeLogRecord r) =>
        new[] { r.Message, r.Exception?.ToString() ?? string.Empty }
            .Concat(r.StructuredState?.Select(kv => kv.Value ?? string.Empty) ?? []);

    // Sent once a consumer subscribes; NoRoutes until then (R-40). Either way the handler cascaded it once.
    private static IEnumerable<EvaluationCompleted> Cascaded(ITrackedSession session, Guid submissionId) =>
        session.AllRecordsInOrder()
            .Where(r => r.MessageEventType is MessageEventType.Sent or MessageEventType.NoRoutes)
            .Select(r => r.Message)
            .OfType<EvaluationCompleted>()
            .Where(e => e.SubmissionId == submissionId);

    private static IEnumerable<EvaluatePortfolio> EvaluateCommands(ITrackedSession session, Guid submissionId) =>
        session.Sent.MessagesOf<EvaluatePortfolio>().Where(c => c.SubmissionId == submissionId);

    private static byte[] Sample(string file) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", file));

    private static byte[] Csv(params (string AssetClass, string BookValue)[] rows) =>
        Encoding.UTF8.GetBytes(
            Header + string.Concat(rows.Select(r => $"{SubmissionTestData.UniquePositionId()},{r.AssetClass},{r.BookValue},USD,x\n")));

    private static SubmissionAccepted Event(Guid tenantId, DateOnly cutOffDate)
    {
        var now = DateTimeOffset.UtcNow;
        return new SubmissionAccepted(
            Guid.CreateVersion7(now), tenantId, now, Guid.CreateVersion7(now), cutOffDate, now,
            [
                new(PortfolioAssetClass.Cash, 10m), new(PortfolioAssetClass.FixedIncome, 0m),
                new(PortfolioAssetClass.Shares, 5m), new(PortfolioAssetClass.RealEstate, 0m),
            ]);
    }

    private async Task<(ITrackedSession Session, SubmissionReceivedResponse Response)> UploadAsync(
        DevIdentity identity, byte[] content, string cutOffDate)
    {
        using var client = fixture.CreateClient(identity);
        return await SubmissionEvents.UploadTrackedAsync(fixture, client, content, cutOffDate, HttpStatusCode.Created, _ct);
    }

    // Through the durable local queue, the way a cascaded event arrives; waits for the cascaded evaluation too.
    private Task<ITrackedSession> DeliverTrackedAsync(Guid tenantId, SubmissionAccepted @event) =>
        fixture.Factory.Services.TrackActivity().DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync(context =>
                context.PublishAsync(@event, new DeliveryOptions { TenantId = tenantId.ToString() }).AsTask());

    private async Task InvokeForTenantAsync(Guid tenantId, object message)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeForTenantAsync(tenantId.ToString(), message, _ct);
    }

    // Raw SQL as the database owner: sees every row, whatever the query filter.
    private async Task<List<EvaluationDbRow>> EvaluationRowsAsync(Guid submissionId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        var rows = await connection.QueryAsync<EvaluationDbRow>(new CommandDefinition(
            """
            SELECT id AS Id, tenant_id AS TenantId, cut_off_date AS CutOffDate, overall_status AS OverallStatus,
                   total_book_value AS TotalBookValue, policy_version AS PolicyVersion, cash_limit AS CashLimit,
                   fixed_income_limit AS FixedIncomeLimit, shares_limit AS SharesLimit,
                   real_estate_limit AS RealEstateLimit, alert_threshold AS AlertThreshold,
                   rebalance_target_ratio AS RebalanceTargetRatio, proposal_status AS ProposalStatus
            FROM compliance.evaluations WHERE submission_id = @submissionId
            """,
            new { submissionId }, cancellationToken: _ct));
        return rows.ToList();
    }

    // Enum order (D-196): Cash, FixedIncome, Shares, RealEstate.
    private async Task<List<ClassDbRow>> ClassRowsAsync(Guid evaluationId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        var rows = await connection.QueryAsync<ClassDbRow>(new CommandDefinition(
            """
            SELECT asset_class AS AssetClass, tenant_id AS TenantId, book_value AS BookValue, share AS Share,
                   utilization AS Utilization, status AS Status, proposed_book_value AS ProposedBookValue
            FROM compliance.evaluation_classes WHERE evaluation_id = @evaluationId
            ORDER BY array_position(ARRAY['Cash', 'FixedIncome', 'Shares', 'RealEstate'], asset_class::text)
            """,
            new { evaluationId }, cancellationToken: _ct));
        return rows.ToList();
    }

    private async Task<List<Guid>> SnapshotIdsAsync(Guid submissionId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        var rows = await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT submission_id FROM compliance.portfolio_snapshots WHERE submission_id = @submissionId",
            new { submissionId }, cancellationToken: _ct));
        return rows.ToList();
    }

    // Through the Compliance context, so the tenant query filter decides what is visible.
    private async Task<(int Evaluations, int Classes)> VisibleRowCountsAsync(Guid tenantId, Guid evaluationId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<ComplianceDbContext>();
        options.UseModuleNpgsql(fixture.ConnectionString, ComplianceDbContext.Schema)
            .AddInterceptors(TenantStampingInterceptor.Instance);
        await using var db = new ComplianceDbContext(options.Options, tenantContext);
        return (
            await db.Set<EvaluationRow>().CountAsync(e => e.Id == evaluationId, _ct),
            await db.Set<EvaluationClassRow>().CountAsync(c => c.EvaluationId == evaluationId, _ct));
    }

    private sealed record EvaluationDbRow(
        Guid Id, Guid TenantId, DateOnly CutOffDate, string OverallStatus, decimal TotalBookValue, int PolicyVersion,
        decimal CashLimit, decimal FixedIncomeLimit, decimal SharesLimit, decimal RealEstateLimit, decimal AlertThreshold,
        decimal RebalanceTargetRatio, string? ProposalStatus);

    private sealed record ClassDbRow(
        string AssetClass, Guid TenantId, decimal BookValue, decimal Share, decimal Utilization, string Status,
        decimal? ProposedBookValue);
}
