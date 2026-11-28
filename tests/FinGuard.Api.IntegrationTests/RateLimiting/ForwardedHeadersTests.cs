using System.Net;

namespace FinGuard.Api.IntegrationTests.RateLimiting;

// The anonymous rate-limit partition is the client IP, so it shows whether X-Forwarded-For is honoured (D-316).
public sealed class ForwardedHeadersTests(ApiFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ForwardedFor_Enabled_UsesForwardedIpForAnonymousPartition()
    {
        await using var factory = RateLimitingTests.LimitedHost(fixture, reads: 100, anonymous: 1, uploads: 100, forwardedHeaders: true);

        var (first, again, other) = await TwoForwardedCallersAsync(factory);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.Unauthorized), (first, again, other));
    }

    [Fact]
    public async Task ForwardedFor_Disabled_IgnoresHeader()
    {
        await using var factory = RateLimitingTests.LimitedHost(fixture, reads: 100, anonymous: 1, uploads: 100, forwardedHeaders: false);

        var (first, again, other) = await TwoForwardedCallersAsync(factory);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests), (first, again, other));
    }

    // Caller A twice, then caller B once, each with its own X-Forwarded-For (documentation addresses, RFC 5737).
    private async Task<(HttpStatusCode First, HttpStatusCode Again, HttpStatusCode Other)> TwoForwardedCallersAsync(
        ApiFixture.ApiFactory factory)
    {
        using var callerA = factory.CreateClient();
        callerA.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10");
        using var callerB = factory.CreateClient();
        callerB.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.20");

        using var first = await callerA.GetAsync(new Uri(RateLimitingTests.TenantPath, UriKind.Relative), _ct);
        using var again = await callerA.GetAsync(new Uri(RateLimitingTests.TenantPath, UriKind.Relative), _ct);
        using var other = await callerB.GetAsync(new Uri(RateLimitingTests.TenantPath, UriKind.Relative), _ct);
        return (first.StatusCode, again.StatusCode, other.StatusCode);
    }
}
