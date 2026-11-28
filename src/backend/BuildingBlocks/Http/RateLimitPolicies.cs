namespace FinGuard.BuildingBlocks.Http;

/// <summary>Named rate-limit policies an endpoint can require on top of the global limiter (ADR-0016).</summary>
public static class RateLimitPolicies
{
    /// <summary>Token bucket per tenant for portfolio uploads, the expensive path (CSV, evaluation, AI).</summary>
    public const string Uploads = "uploads";
}
