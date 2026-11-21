using FinGuard.Compliance.Features.EvaluatePortfolio;

namespace FinGuard.UnitTests.Compliance;

public sealed class EvaluatePortfolioTests
{
    [Fact]
    public void ToString_Always_PrintsSubmissionIdOnly()
    {
        var submissionId = Guid.NewGuid();

        var text = new EvaluatePortfolio(submissionId).ToString();

        Assert.Equal($"EvaluatePortfolio {{ SubmissionId = {submissionId} }}", text);
    }
}
