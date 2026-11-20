using FinGuard.Portfolio.Features.ReceiveSubmission;

namespace FinGuard.UnitTests.Portfolio;

public sealed class ReceiveSubmissionTests
{
    [Fact]
    public void ToString_Always_PrintsSizeOnly()
    {
        var command = new ReceiveSubmission(new DateOnly(2031, 7, 19), "secret-marker-file.csv", new byte[42]);

        var text = command.ToString();

        Assert.Equal("ReceiveSubmission { SizeBytes = 42 }", text);
        Assert.DoesNotContain("2031", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-marker-file", text, StringComparison.Ordinal);
    }
}
