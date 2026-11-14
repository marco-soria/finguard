using System.Net;

namespace FinGuard.Api.IntegrationTests;

public sealed class HealthTests(ApiFixture fixture)
{
    [Fact]
    public async Task Health_DatabaseUp_Returns200()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
