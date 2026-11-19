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
    public void Handle_ValidUpload_AddsSubmissionAndContent(int size, string fileName, string cutOffDate)
    {
        using var db = CreateContext();

        var result = Handle(db, size, fileName, cutOffDate);

        Assert.True(result.IsSuccess);
        Assert.Equal(("Received", Now), (result.Value.Status, result.Value.ReceivedAt));
        var submission = Assert.Single(db.ChangeTracker.Entries<Submission>()).Entity;
        var content = Assert.Single(db.ChangeTracker.Entries<SubmissionContent>()).Entity;
        Assert.Equal((result.Value.SubmissionId, result.Value.SubmissionId), (submission.Id, content.SubmissionId));
        Assert.Equal((size, 64), (submission.SizeBytes, submission.Sha256.Length));
    }

    private static BuildingBlocks.Results.Result<SubmissionReceivedResponse> Handle(
        PortfolioDbContext db, int size, string fileName, string cutOffDate)
    {
        var handler = new ReceiveSubmissionHandler(db, new FakeTimeProvider(Now), NullLogger<ReceiveSubmissionHandler>.Instance);
        return handler.Handle(new ReceiveSubmission(DateOnly.Parse(cutOffDate, System.Globalization.CultureInfo.InvariantCulture), fileName, new byte[size]));
    }

    // Never connects: the handler only adds entities; Wolverine's transaction middleware saves them.
    private static PortfolioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>();
        options.UseModuleNpgsql(DesignTimeConnection.Placeholder, PortfolioDbContext.Schema);
        return new PortfolioDbContext(options.Options, NoTenantContext.Instance);
    }
}
