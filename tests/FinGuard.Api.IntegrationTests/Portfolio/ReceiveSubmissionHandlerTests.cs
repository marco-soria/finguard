using System.Globalization;
using System.Text;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Portfolio.Features.ReceiveSubmission;
using FinGuard.Portfolio.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace FinGuard.Api.IntegrationTests.Portfolio;

/// <summary>
/// Handler-level tests on real PostgreSQL: the duplicate pre-check (D-139) queries the database, so a placeholder
/// connection is not enough. Each test uses fresh tenant ids, so the shared database never makes a case a duplicate.
/// SaveChanges stands in for Wolverine's transaction middleware.
/// </summary>
public sealed class ReceiveSubmissionHandlerTests(ApiFixture fixture)
{
    private const string Header = "position_id,asset_class,book_value,currency,description\n";
    private static readonly DateTimeOffset Now = new(2026, 3, 15, 23, 30, 0, TimeSpan.Zero);
    private static readonly byte[] ValidCsv = Encoding.UTF8.GetBytes(Header + "P-1,cash,100.25,USD,x\nP-2,real_estate,50,USD,y\n");
    private static readonly byte[] InvalidCsv = Encoding.UTF8.GetBytes(Header + "P-1,cash,-1,USD,x\nP-2,crypto,5,EUR,y\n");

    private readonly FakeTimeProvider _time = new(Now);

    // The handler reads the tenant for the event payload; it must be the one its context stamps.
    private readonly Dictionary<PortfolioDbContext, TenantContext> _tenants = [];

    [Theory]
    [InlineData(10, "portfolio.CSV", "2026-03-15")]
    [InlineData(5 * 1024 * 1024, "portfolio.csv", "2026-03-15")]
    [InlineData(10, "portfolio.csv", "2025-12-31")]
    public async Task Handle_TransportValidUpload_AddsSubmissionAndContent(int size, string fileName, string cutOffDate)
    {
        await using var db = CreateContext(Guid.NewGuid());

        // Zero bytes pass the transport checks but are no portfolio CSV: the submission is stored as Rejected.
        var result = await Handle(db, new byte[size], fileName, cutOffDate);

        Assert.True(result.IsSuccess);
        Assert.Equal(("Rejected", Now, false), (result.Value.Status, result.Value.ReceivedAt, result.Value.Duplicate));
        var submission = Assert.Single(db.ChangeTracker.Entries<Submission>()).Entity;
        var content = Assert.Single(db.ChangeTracker.Entries<SubmissionContent>()).Entity;
        Assert.Equal((result.Value.SubmissionId, result.Value.SubmissionId), (submission.Id, content.SubmissionId));
        Assert.Equal((size, 64), (submission.SizeBytes, submission.Sha256.Length));
    }

    [Fact]
    public async Task Handle_ValidCsv_AddsAcceptedSubmissionWithPositions()
    {
        await using var db = CreateContext(Guid.NewGuid());

        var result = await Handle(db, ValidCsv);

        Assert.Equal(("Accepted", 2, 0), (result.Value.Status, result.Value.RowCount, result.Value.ErrorCount));
        Assert.Empty(result.Value.Errors);
        var submission = Assert.Single(db.ChangeTracker.Entries<Submission>()).Entity;
        Assert.Equal((SubmissionStatus.Accepted, 2, 0), (submission.Status, submission.RowCount, submission.ErrorCount));
        var positions = db.ChangeTracker.Entries<Position>().Select(e => e.Entity).OrderBy(p => p.RowNumber).ToList();
        Assert.Equal(
            [(2, "P-1", AssetClass.Cash, 100.25m), (3, "P-2", AssetClass.RealEstate, 50m)],
            positions.Select(p => (p.RowNumber, p.PositionId, p.AssetClass, p.BookValue)));
        Assert.All(positions, p => Assert.Equal(submission.Id, p.SubmissionId));
        Assert.Empty(db.ChangeTracker.Entries<SubmissionError>());
    }

    [Fact]
    public async Task Handle_InvalidCsv_AddsRejectedSubmissionWithErrors()
    {
        await using var db = CreateContext(Guid.NewGuid());

        var result = await Handle(db, InvalidCsv);

        Assert.Equal(("Rejected", 2, 3), (result.Value.Status, result.Value.RowCount, result.Value.ErrorCount));
        Assert.Equal(3, result.Value.Errors.Count);
        var submission = Assert.Single(db.ChangeTracker.Entries<Submission>()).Entity;
        Assert.Equal((SubmissionStatus.Rejected, 3), (submission.Status, submission.ErrorCount));
        Assert.Single(db.ChangeTracker.Entries<SubmissionContent>());
        Assert.Empty(db.ChangeTracker.Entries<Position>());
        var errors = db.ChangeTracker.Entries<SubmissionError>().Select(e => e.Entity).OrderBy(e => e.Ordinal).ToList();
        Assert.Equal(
            [(0, 2, "book_value", "Portfolio.Csv.BookValueNegative"),
             (1, 3, "asset_class", "Portfolio.Csv.AssetClassUnknown"),
             (2, 3, "currency", "Portfolio.Csv.CurrencyNotSupported")],
            errors.Select(e => (e.Ordinal, e.RowNumber!.Value, e.Column!, e.Code)));
    }

    [Fact]
    public async Task Handle_DuplicateOfAccepted_ReturnsExistingAndAddsNothing()
    {
        var tenant = Guid.NewGuid();
        var first = await HandleAndSave(tenant, ValidCsv);
        _time.Advance(TimeSpan.FromMinutes(1));
        await using var db = CreateContext(tenant);

        var result = await Handle(db, ValidCsv, fileName: "renamed.csv");

        Assert.Equal(
            (first.SubmissionId, "Accepted", first.ReceivedAt, 2, true),
            (result.Value.SubmissionId, result.Value.Status, result.Value.ReceivedAt, result.Value.RowCount, result.Value.Duplicate));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Handle_DuplicateOfRejected_ReturnsRejectedWithItsErrors()
    {
        var tenant = Guid.NewGuid();
        var first = await HandleAndSave(tenant, InvalidCsv);
        await using var db = CreateContext(tenant);

        var result = await Handle(db, InvalidCsv);

        Assert.Equal(
            (first.SubmissionId, "Rejected", 3, true),
            (result.Value.SubmissionId, result.Value.Status, result.Value.ErrorCount, result.Value.Duplicate));
        Assert.Equal(first.Errors, result.Value.Errors);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Handle_SameBytesOtherCutOffDate_IsNew()
    {
        var tenant = Guid.NewGuid();
        var first = await HandleAndSave(tenant, ValidCsv, "2026-03-14");
        await using var db = CreateContext(tenant);

        var result = await Handle(db, ValidCsv, cutOffDate: "2026-03-15");

        Assert.False(result.Value.Duplicate);
        Assert.NotEqual(first.SubmissionId, result.Value.SubmissionId);
        Assert.Single(db.ChangeTracker.Entries<Submission>());
    }

    [Fact]
    public async Task Handle_SameBytesOtherTenant_IsNew()
    {
        var first = await HandleAndSave(Guid.NewGuid(), ValidCsv);
        await using var db = CreateContext(Guid.NewGuid());

        var result = await Handle(db, ValidCsv);

        Assert.False(result.Value.Duplicate);
        Assert.NotEqual(first.SubmissionId, result.Value.SubmissionId);
        Assert.Single(db.ChangeTracker.Entries<Submission>());
    }

    private async Task<SubmissionReceivedResponse> HandleAndSave(Guid tenantId, byte[] content, string cutOffDate = "2026-03-15")
    {
        await using var db = CreateContext(tenantId);
        var result = await Handle(db, content, cutOffDate: cutOffDate);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return result.Value;
    }

    private async Task<Result<SubmissionReceivedResponse>> Handle(
        PortfolioDbContext db, byte[] content, string fileName = "portfolio.csv", string cutOffDate = "2026-03-15")
    {
        var handler = new ReceiveSubmissionHandler(
            db, _tenants[db], _time, NullLogger<ReceiveSubmissionHandler>.Instance);
        var date = DateOnly.Parse(cutOffDate, CultureInfo.InvariantCulture);
        var (result, _) = await handler.Handle(
            new ReceiveSubmission(date, fileName, content), TestContext.Current.CancellationToken);
        return result;
    }

    private PortfolioDbContext CreateContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PortfolioDbContext>();
        options.UseModuleNpgsql(fixture.ConnectionString, PortfolioDbContext.Schema)
            .AddInterceptors(TenantStampingInterceptor.Instance);
        var db = new PortfolioDbContext(options.Options, tenantContext);
        _tenants[db] = tenantContext;
        return db;
    }
}
