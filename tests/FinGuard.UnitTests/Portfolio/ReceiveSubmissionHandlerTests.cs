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
    [InlineData(10, "portfolio.csv", "1999-12-31", "Portfolio.Submission.CutOffDateTooEarly")]
    [InlineData(10, "portfolio.txt", "1999-12-31", "Portfolio.Submission.NotCsv")]
    [InlineData(0, "portfolio.txt", "2026-03-16", "Portfolio.Submission.FileEmpty")]
    public async Task Handle_InvalidUpload_ReturnsValidationCode(int size, string fileName, string cutOffDate, string code)
    {
        using var db = CreateContext();

        var result = await Handle(db, size, fileName, cutOffDate);

        Assert.True(result.IsFailure);
        Assert.Equal((code, BuildingBlocks.Results.ErrorType.Validation), (result.Error.Code, result.Error.Type));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public void Validate_CutOffOnFirstPolicyDate_PassesTransportChecks()
    {
        var error = ReceiveSubmissionHandler.Validate(
            new ReceiveSubmission(new DateOnly(2000, 1, 1), "portfolio.csv", new byte[10]), DateOnly.FromDateTime(Now.UtcDateTime));

        Assert.Null(error);
    }

    private static async Task<BuildingBlocks.Results.Result<SubmissionReceivedResponse>> Handle(
        PortfolioDbContext db, int size, string fileName, string cutOffDate)
    {
        var handler = new ReceiveSubmissionHandler(
            db, NoTenantContext.Instance, new FakeTimeProvider(Now), NullLogger<ReceiveSubmissionHandler>.Instance);
        var date = DateOnly.Parse(cutOffDate, System.Globalization.CultureInfo.InvariantCulture);
        var (result, outgoing) = await handler.Handle(
            new ReceiveSubmission(date, fileName, new byte[size]), TestContext.Current.CancellationToken);
        Assert.Empty(outgoing);
        return result;
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
