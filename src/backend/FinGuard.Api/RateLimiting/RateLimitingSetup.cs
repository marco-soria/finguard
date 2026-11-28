using System.Globalization;
using System.Threading.RateLimiting;
using FinGuard.BuildingBlocks.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FinGuard.Api.RateLimiting;

/// <summary>
/// Built-in ASP.NET Core rate limiting (ADR-0016, D-313). A global limiter charges every request to its tenant or, without
/// one, to its client IP; the <see cref="RateLimitPolicies.Uploads"/> policy adds a token bucket per tenant on top.
/// Counters live in this process's memory: with N replicas the effective limit is N times the configured one (R-54).
/// </summary>
public static partial class RateLimitingSetup
{
    public const string GlobalPolicy = "global";
    public const string ExceededCode = "RateLimit.Exceeded";
    public const string ExceededTitle = "Too many requests";

    // RFC 6585 defines 429; ASP.NET Core's ProblemDetails defaults have no type for it.
    private const string ExceededType = "https://tools.ietf.org/html/rfc6585#section-4";

    // The partitioner of the limiter that runs last records itself here, so the rejection log can name it: the endpoint
    // policy only runs after the global limiter granted a permit.
    private static readonly object RejectionSourceKey = new();

    public static IHostApplicationBuilder AddFinGuardRateLimiting(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var limits = Limits(context);
                var partition = Charge(context, GlobalPolicy);
                var permits = partition.Kind == RequestPartition.TenantKind ? limits.ReadsPerMinute : limits.AnonymousPerMinute;
                return RateLimitPartition.GetFixedWindowLimiter(partition.Key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
            options.AddPolicy(RateLimitPolicies.Uploads, context =>
            {
                var limits = Limits(context);
                var partition = Charge(context, RateLimitPolicies.Uploads);
                return RateLimitPartition.GetTokenBucketLimiter(partition.Key, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = limits.UploadsPerMinute,
                    TokensPerPeriod = limits.UploadsPerMinute,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
            options.OnRejected = OnRejectedAsync;
        });
        return builder;
    }

    /// <summary>The <c>Retry-After</c> value: whole seconds, rounded up, at least 1.</summary>
    public static int RetryAfterSeconds(TimeSpan delay) => Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds));

    private static RateLimitingOptions Limits(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    private static RequestPartition Charge(HttpContext context, string policy)
    {
        var partition = RequestPartition.For(context);
        context.Items[RejectionSourceKey] = (policy, partition.Kind);
        return partition;
    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext rejected, CancellationToken _)
    {
        var context = rejected.HttpContext;
        if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay))
        {
            context.Response.Headers.RetryAfter = RetryAfterSeconds(delay).ToString(CultureInfo.InvariantCulture);
        }

        var (policy, kind) = context.Items[RejectionSourceKey] as (string, string)? ?? (GlobalPolicy, RequestPartition.IpKind);
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RateLimitingSetup));
        // Kind and policy only: never the tenant id, the IP or the path (D-315).
        LogRejected(logger, policy, kind);

        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Type = ExceededType,
                Title = ExceededTitle,
                Detail = "The request limit was reached. Try again later.",
                Extensions = { ["code"] = ExceededCode },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Request rejected by rate limit policy {Policy} ({PartitionKind} partition)")]
    private static partial void LogRejected(ILogger logger, string policy, string partitionKind);
}
