using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinGuard.Api.IntegrationTests.Tenancy;

// A second Api host in Production on the shared database (R-17: the development issuer must not leak).
public sealed class ProductionEnvironmentTests(ApiFixture fixture)
{
    [Fact]
    public async Task DevToken_ProductionEnvironment_IsNotServed()
    {
        await using var factory = new ProductionFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/dev/token", UriKind.Relative), new { user = "alice" }, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // No /dev/token endpoint exists, so the fallback policy answers before routing could return 404.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ProductionFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:finguard", connectionString);
            builder.UseSetting("KeyVault:Uri", string.Empty);
            builder.UseSetting("APPLICATIONINSIGHTS_CONNECTION_STRING", string.Empty);
            // Never contacted: the request carries no token, so no metadata is fetched.
            builder.UseSetting("Authentication:Schemes:Bearer:Authority", "https://login.invalid/finguard-tests");
            builder.UseSetting("Authentication:Schemes:Bearer:Audience", "finguard-api");
        }
    }
}
