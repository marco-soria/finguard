using System.Buffers.Binary;
using System.Buffers.Text;

namespace FinGuard.Portfolio.Features.ListSubmissions;

/// <summary>
/// Opaque keyset cursor: base64url of a version byte, the last item's <c>received_at</c> (UTC ticks) and its id.
/// Not signed: the query's tenant filter is the guard, so an edited or foreign cursor only moves the position inside
/// the caller's own rows (Brief #20).
/// </summary>
internal readonly record struct SubmissionCursor(DateTimeOffset ReceivedAt, Guid Id)
{
    private const byte Version = 1;
    private const int Length = 1 + sizeof(long) + 16;

    public string Encode()
    {
        Span<byte> bytes = stackalloc byte[Length];
        bytes[0] = Version;
        BinaryPrimitives.WriteInt64BigEndian(bytes[1..], ReceivedAt.UtcTicks);
        Id.TryWriteBytes(bytes[9..], bigEndian: true, out _);
        return Base64Url.EncodeToString(bytes);
    }

    public static bool TryDecode(string? value, out SubmissionCursor cursor)
    {
        cursor = default;
        // Canonical base64url of exactly Length bytes is 34 characters; anything else is malformed.
        // TryDecodeFromChars throws on characters outside the alphabet, so IsValid runs first.
        if (value is null || value.Length != Base64Url.GetEncodedLength(Length) || !Base64Url.IsValid(value))
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[Length];
        if (!Base64Url.TryDecodeFromChars(value, bytes, out var written) || written != Length || bytes[0] != Version
            || Base64Url.EncodeToString(bytes) != value)
        {
            return false;
        }

        var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes[1..]);
        if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return false;
        }

        cursor = new SubmissionCursor(new DateTimeOffset(ticks, TimeSpan.Zero), new Guid(bytes[9..], bigEndian: true));
        return true;
    }
}
