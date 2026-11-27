using FinGuard.BuildingBlocks.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FinGuard.Api.IntegrationTests;

/// <summary>D-302: every API endpoint lives under <c>/api/v1/</c>; only infrastructure routes stay outside.</summary>
public sealed class RouteTests(ApiFixture fixture)
{
    // Health checks, the Development token issuer, the API docs, and the probes the test host adds.
    private static readonly string[] AllowedOutsideV1 = ["/health", "/alive", "/dev/token", "/openapi/", "/scalar", "/api/_test/"];

    [Fact]
    public void MappedEndpoints_Always_StartWithApiV1UnlessAllowListed()
    {
        var patterns = fixture.Factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => "/" + endpoint.RoutePattern.RawText!.TrimStart('/'))
            .ToList();

        var outside = patterns
            .Where(p => !p.StartsWith(ApiRoutes.V1 + "/", StringComparison.Ordinal))
            .Where(p => !AllowedOutsideV1.Any(allowed => p.StartsWith(allowed, StringComparison.Ordinal)))
            .ToList();

        Assert.Contains(patterns, p => p.StartsWith(ApiRoutes.V1 + "/", StringComparison.Ordinal));
        Assert.Empty(outside);
    }
}
