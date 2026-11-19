using System.Text;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Portfolio.Features.ReceiveSubmission;
using FinGuard.Portfolio.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace FinGuard.UnitTests.Portfolio;

public sealed class ReceiveSubmissionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 15, 23, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "portfolio.csv", "2026-03-15", "Portfolio.Submission.FileEmpty")]
    [InlineData((5 * 1024 * 1024) + 1, "portfolio.csv", "2026-03-15", "Portfolio.Submission.FileTooLarge")]
    [InlineData(10, "portfolio.txt", "2026-03-15", "Portfolio.Submission.NotCsv")]
    [InlineData(10, "portfolio", "2026-03-15", "Portfolio.Submission.NotCsv")]
    [InlineData(10, "portfolio.csv", "2026-03-16", "Portfolio.Submission.CutOffDateInFuture")]
    [InlineData(0, "portfolio.txt", "2026-03-16", "Portfolio.Submission.FileEmpty")]
    public void Handle_InvalidUpload_ReturnsValidationCode(int size, string fileName, string cutOffDate, string code)
    {
        using var db = CreateContext();

        var result = Handle(db, size, fileName, cutOffDate);

        Assert.True(result.IsFailure);
        Assert.Equal((code, BuildingBlocks.Results.ErrorType.Validation), (result.Error.Code, result.Error.Type));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(10, "portfolio.CSV", "2026-03-15")]
    [InlineData(5 * 1024 * 1024, "portfolio.csv", "2026-03-15")]
    [InlineData(10, "portfolio.csv", "2025-12-31")]
    public void Handle_TransportValidUpload_AddsSubmissionAndContent(int size, string fileName, string cutOffDate)
    {
        using var db = CreateContext();

        // Zero bytes pass the transport checks but are no portfolio CSV: the submission is stored as Rejected.
        var result = Handle(db, new byte[size], fileName, cutOffDate);

        Assert.True(result.IsSuccess);
        Assert.Equal(("Rejected", Now), (result.Value.Status, result.Value.ReceivedAt));
        var submission = Assert.Single(db.ChangeTracker.Entries<Submission>()).Entity;
        var content = Assert.Single(db.ChangeTracker.Entries<SubmissionContent>()).Entity;
        Assert.Equal((result.Value.SubmissionId, result.Value.SubmissionId), (submission.Id, content.SubmissionId));
        Assert.Equal((size, 64), (submission.SizeBytes, submission.Sha256.Length));
    }

    [Fact]
    public void Handle_ValidCsv_AddsAcceptedSubmissionWithPositions()
    {
        using var db = CreateContext();
        var csv = Encoding.UTF8.GetBytes(
            "position_id,asset_class,book_value,currency,description\nP-1,cash,100.25,USD,x\nP-2,real_estate,50,USD,y\n");

        var result = Handle(db, csv, "portfolio.csv", "2026-03-15");

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
    public void Handle_InvalidCsv_AddsRejectedSubmissionWithErrors()
    {
        using var db = CreateContext();
        var csv = Encoding.UTF8.GetBytes(
            "position_id,asset_class,book_value,currency,description\nP-1,cash,-1,USD,x\nP-2,crypto,5,EUR,y\n");

        var result = Handle(db, csv, "portfolio.csv", "2026-03-15");

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

    private static BuildingBlocks.Results.Result<SubmissionReceivedResponse> Handle(
        PortfolioDbContext db, int size, string fileName, string cutOffDate) =>
        Handle(db, new byte[size], fileName, cutOffDate);

    private static BuildingBlocks.Results.Result<SubmissionReceivedResponse> Handle(
        PortfolioDbContext db, byte[] content, string fileName, string cutOffDate)
    {
        var handler = new ReceiveSubmissionHandler(db, new FakeTimeProvider(Now), NullLogger<ReceiveSubmissionHandler>.Instance);
        return handler.Handle(new ReceiveSubmission(DateOnly.Parse(cutOffDate, System.Globalization.CultureInfo.InvariantCulture), fileName, content));
    }

    // Never connects: the handler only adds entities; Wolverine's transaction middleware saves them.
    private static PortfolioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>();
        options.UseModuleNpgsql(DesignTimeConnection.Placeholder, PortfolioDbContext.Schema);
        return new PortfolioDbContext(options.Options, NoTenantContext.Instance);
    }
}
