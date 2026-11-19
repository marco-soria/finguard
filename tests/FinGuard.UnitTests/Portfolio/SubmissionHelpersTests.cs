using System.Text;
using FinGuard.Portfolio.Features.ReceiveSubmission;

namespace FinGuard.UnitTests.Portfolio;

public sealed class SubmissionHelpersTests
{
    public static TheoryData<string, string> UnsafeNames => new()
    {
        { "../x.csv", "x.csv" },
        { "..\\..\\x.csv", "x.csv" },
        { @"C:\a\b.csv", "b.csv" },
        { "/etc/passwd.csv", "passwd.csv" },
        { "a\u0000b\u001Fc\r\n.csv", "abc.csv" },
        { "  report.csv  ", "report.csv" },
        { new string('a', 296) + ".csv", new string('a', 255) },
    };

    [Theory]
    [MemberData(nameof(UnsafeNames))]
    public void Sanitize_PathTraversalOrControlChars_ReturnsSafeName(string input, string expected)
    {
        var name = FileNameSanitizer.Sanitize(input);

        Assert.Equal(expected, name);
    }

    [Fact]
    public void Compute_KnownInput_ReturnsKnownHex()
    {
        // FIPS 180-2 test vector for "abc".
        var hex = Sha256Hex.Compute(Encoding.ASCII.GetBytes("abc"));

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hex);
    }
}
