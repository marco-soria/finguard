using System.Security.Cryptography;

namespace FinGuard.Portfolio.Features.ReceiveSubmission;

internal static class Sha256Hex
{
    public static string Compute(ReadOnlySpan<byte> content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
