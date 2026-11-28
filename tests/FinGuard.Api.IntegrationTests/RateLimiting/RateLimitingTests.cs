using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FinGuard.Api.DevAuth;
using FinGuard.Api.IntegrationTests.Portfolio;
using FinGuard.Api.RateLimiting;
using FinGuard.Tenancy.Development;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;

namespace FinGuard.Api.IntegrationTests.RateLimiting;

// Each test boots its own host: counters live in the host's memory, so a fresh host starts from zero (ADR-0016).
public sealed class RateLimitingTests(ApiFixture fixture)
{
    internal const string TenantPath = "/api/v1/tenancy/me";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_OverTenantLimit_Returns429WithRetryAfter()
    {
        await using var factory = Host(uploads: 2);
        using var alice = Client(factory, DevIdentities.Alice);

        await alice.UploadOkAsync("one.csv", _ct);
        await alice.UploadOkAsync("two.csv", _ct);
        using var response = await alice.UploadAsync("three.csv", _ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        Assert.Equal(RateLimitingSetup.ExceededCode, problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(RateLimitingSetup.ExceededTitle, problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(429, problem.RootElement.GetProperty("status").GetInt32());
        // Kind and policy only, never the tenant id (D-315).
        var log = Assert.Single(factory.Services.GetFakeLogCollector().GetSnapshot(),
            record => record.Message.StartsWith("Request rejected by rate limit", StringComparison.Ordinal));
        Assert.Equal("Request rejected by rate limit policy uploads (tenant partition)", log.Message);
    }

    [Fact]
    public async Task Upload_LimitIsPerTenant()
    {
        await using var factory = Host(uploads: 1);
        using var alice = Client(factory, DevIdentities.Alice);
        using var bob = Client(factory, DevIdentities.Bob);

        await alice.UploadOkAsync("alice.csv", _ct);
        using var aliceAgain = await alice.UploadAsync("alice-again.csv", _ct);
        using var bobFirst = await bob.UploadAsync("bob.csv", _ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, aliceAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Created, bobFirst.StatusCode);
    }

    [Fact]
    public async Task Reads_OverLimit_Return429()
    {
        await using var factory = Host(reads: 5);
        using var alice = Client(factory, DevIdentities.Alice);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            using var response = await alice.GetAsync(new Uri(TenantPath, UriKind.Relative), _ct);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([.. Enumerable.Repeat(HttpStatusCode.OK, 5), HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task Anonymous_OverIpLimit_Return429()
    {
        await using var factory = Host(anonymous: 3);
        using var anonymous = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            using var response = await anonymous.GetAsync(new Uri(TenantPath, UriKind.Relative), _ct);
            statuses.Add(response.StatusCode);
        }

        // The limiter runs before authorization: once the limit is hit, 429 comes before the 401.
        Assert.Equal([.. Enumerable.Repeat(HttpStatusCode.Unauthorized, 3), HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task Health_IsNeverRateLimited()
    {
        await using var factory = Host(anonymous: 1);
        using var anonymous = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        foreach (var path in new[] { "/health", "/alive", "/health", "/alive" })
        {
            using var response = await anonymous.GetAsync(new Uri(path, UriKind.Relative), _ct);
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    private ApiFixture.ApiFactory Host(int reads = 100, int anonymous = 100, int uploads = 100) =>
        LimitedHost(fixture, reads, anonymous, uploads, forwardedHeaders: false);

    /// <summary>A host of its own with the given limits; shared with the forwarded-headers tests.</summary>
    internal static ApiFixture.ApiFactory LimitedHost(ApiFixture fixture, int reads, int anonymous, int uploads, bool forwardedHeaders) =>
        new(fixture.ConnectionString, builder => builder
            .UseSetting("RateLimiting:ReadsPerMinute", reads.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .UseSetting("RateLimiting:AnonymousPerMinute", anonymous.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .UseSetting("RateLimiting:UploadsPerMinute", uploads.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .UseSetting("ForwardedHeaders:Enabled", forwardedHeaders ? "true" : "false"));

    private static HttpClient Client(ApiFixture.ApiFactory factory, DevIdentity identity)
    {
        var token = factory.Services.GetRequiredService<DevTokenIssuer>().CreateToken(identity, TimeProvider.System.GetUtcNow());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }
}
