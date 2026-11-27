using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinGuard.Tenancy.Development;
using FinGuard.Tenancy.Features.GetCurrentTenant;

namespace FinGuard.Api.IntegrationTests.Tenancy;

// Reference slice (D-81): Minimal API -> Wolverine query handler -> Dapper, tenant from ITenantContext.
public sealed class GetCurrentTenantTests(ApiFixture fixture)
{
    private const string Path = "/api/v1/tenancy/me";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetCurrentTenant_AliceToken_ReturnsAcme()
    {
        var tenant = await GetAsync(DevIdentities.Alice, Path);

        Assert.Equal(new CurrentTenantResponse(DevIdentities.Alice.TenantId, "Acme Pension Fund"), tenant);
    }

    [Fact]
    public async Task GetCurrentTenant_BobToken_ReturnsGlobex()
    {
        var tenant = await GetAsync(DevIdentities.Bob, Path);

        Assert.Equal(new CurrentTenantResponse(DevIdentities.Bob.TenantId, "Globex Insurance"), tenant);
    }

    [Fact]
    public async Task GetCurrentTenant_NoToken_Returns401()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(new Uri(Path, UriKind.Relative), _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentTenant_UnprovisionedIdentity_Returns403WithCode()
    {
        var stranger = new DevIdentity("mallory", Guid.NewGuid().ToString(), "Mallory", Guid.Empty, "None");
        using var client = fixture.CreateClient(stranger);

        using var response = await client.GetAsync(new Uri(Path, UriKind.Relative), _ct);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Tenancy.User.NotProvisioned", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetCurrentTenant_TenantIdInQuery_IsIgnored()
    {
        // AGENTS.md rule 7: the tenant never comes from the request.
        var tenant = await GetAsync(DevIdentities.Alice, $"{Path}?tenantId={DevIdentities.Bob.TenantId}");

        Assert.Equal(DevIdentities.Alice.TenantId, tenant.TenantId);
        Assert.Equal("Acme Pension Fund", tenant.TenantName);
    }

    private async Task<CurrentTenantResponse> GetAsync(DevIdentity identity, string path)
    {
        using var client = fixture.CreateClient(identity);
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), _ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CurrentTenantResponse>(_ct))!;
    }
}
