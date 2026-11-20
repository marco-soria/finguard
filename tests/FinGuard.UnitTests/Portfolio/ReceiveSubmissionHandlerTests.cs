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
    public async Task Handle_InvalidUpload_ReturnsValidationCode(int size, string fileName, string cutOffDate, string code)
    {
        using var db = CreateContext();

        var result = await Handle(db, size, fileName, cutOffDate);

        Assert.True(result.IsFailure);
        Assert.Equal((code, BuildingBlocks.Results.ErrorType.Validation), (result.Error.Code, result.Error.Type));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private static Task<BuildingBlocks.Results.Result<SubmissionReceivedResponse>> Handle(
        PortfolioDbContext db, int size, string fileName, string cutOffDate)
    {
        var handler = new ReceiveSubmissionHandler(db, new FakeTimeProvider(Now), NullLogger<ReceiveSubmissionHandler>.Instance);
        var date = DateOnly.Parse(cutOffDate, System.Globalization.CultureInfo.InvariantCulture);
        return handler.Handle(new ReceiveSubmission(date, fileName, new byte[size]), TestContext.Current.CancellationToken);
    }

    // Never connects: transport failures return before the duplicate pre-check. Cases that reach the database live in
    // FinGuard.Api.IntegrationTests (ReceiveSubmissionHandlerTests).
    private static PortfolioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>();
        options.UseModuleNpgsql(DesignTimeConnection.Placeholder, PortfolioDbContext.Schema);
        return new PortfolioDbContext(options.Options, NoTenantContext.Instance);
    }
}
