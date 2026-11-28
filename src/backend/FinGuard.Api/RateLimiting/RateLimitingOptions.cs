using System.ComponentModel.DataAnnotations;

namespace FinGuard.Api.RateLimiting;

/// <summary>
/// Limits from the <c>RateLimiting</c> section, validated on start (D-314). No defaults in code: <c>appsettings.json</c>
/// holds them, so a missing section fails the start instead of running without limits.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests per minute for each tenant (fixed window), every endpoint.</summary>
    [Range(1, int.MaxValue)]
    public int ReadsPerMinute { get; set; }

    /// <summary>Requests per minute for each client IP without a tenant (fixed window).</summary>
    [Range(1, int.MaxValue)]
    public int AnonymousPerMinute { get; set; }

    /// <summary>Upload token bucket per tenant: its size and its refill per minute.</summary>
    [Range(1, int.MaxValue)]
    public int UploadsPerMinute { get; set; }
}
