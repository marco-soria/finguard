using System.Buffers.Text;
using FinGuard.Portfolio.Features.ListSubmissions;

namespace FinGuard.UnitTests.Portfolio;

public sealed class SubmissionCursorTests
{
    private static readonly SubmissionCursor Sample = new(
        new DateTimeOffset(2026, 11, 27, 10, 15, 30, 123, 456, TimeSpan.Zero), Guid.Parse("0b9f6a52-3d1e-4c7a-9a43-2f1c5e8d7b10"));

    [Fact]
    public void Encode_ThenDecode_ReturnsTheSamePosition()
    {
        var encoded = Sample.Encode();

        Assert.True(SubmissionCursor.TryDecode(encoded, out var decoded));
        Assert.Equal(Sample, decoded);
    }

    [Fact]
    public void Encode_Always_IsUrlSafeWithoutPadding()
    {
        var encoded = Sample.Encode();

        Assert.Matches("^[A-Za-z0-9_-]+$", encoded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cursor")]
    [InlineData("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==")]
    public void TryDecode_Malformed_ReturnsFalse(string value) => Assert.False(SubmissionCursor.TryDecode(value, out _));

    [Fact]
    public void TryDecode_Null_ReturnsFalse() => Assert.False(SubmissionCursor.TryDecode(null, out _));

    [Fact]
    public void TryDecode_UnknownVersion_ReturnsFalse()
    {
        var bytes = Base64Url.DecodeFromChars(Sample.Encode());
        bytes[0] = 2;

        Assert.False(SubmissionCursor.TryDecode(Base64Url.EncodeToString(bytes), out _));
    }

    [Fact]
    public void TryDecode_TicksOutOfRange_ReturnsFalse()
    {
        var bytes = Base64Url.DecodeFromChars(Sample.Encode());
        bytes.AsSpan(1, 8).Fill(0xFF);

        Assert.False(SubmissionCursor.TryDecode(Base64Url.EncodeToString(bytes), out _));
    }

    [Fact]
    public void TryDecode_Truncated_ReturnsFalse() =>
        Assert.False(SubmissionCursor.TryDecode(Sample.Encode()[..^2], out _));

    [Fact]
    public void TryDecode_ExtraBytes_ReturnsFalse()
    {
        var bytes = Base64Url.DecodeFromChars(Sample.Encode()).Append((byte)0).ToArray();

        Assert.False(SubmissionCursor.TryDecode(Base64Url.EncodeToString(bytes), out _));
    }
}
