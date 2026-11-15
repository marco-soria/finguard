using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FinGuard.Tenancy.Development;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FinGuard.Api.IntegrationTests.Tenancy;

public sealed class AuthenticationTests(ApiFixture fixture)
{
    private static readonly Uri ProbeUri = new(TenantProbeStartupFilter.Path, UriKind.Relative);
    private static readonly Uri DevTokenUri = new("/dev/token", UriKind.Relative);

    [Fact]
    public async Task Health_NoToken_Returns200()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/alive", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_NoToken_Returns401()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(ProbeUri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_NoToken_Returns401()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/no-such-route", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_TokenSignedWithOtherKey_Returns401()
    {
        var otherKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = DevIdentities.Issuer,
            Audience = DevIdentities.Audience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, DevIdentities.Alice.Subject)]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddHours(1),
            SigningCredentials = new SigningCredentials(otherKey, SecurityAlgorithms.HmacSha256),
        });
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync(ProbeUri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_ExpiredToken_Returns401()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice, issuedAt: DateTimeOffset.UtcNow.AddHours(-2));

        using var response = await client.GetAsync(ProbeUri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_AliceToken_ResolvesAcme()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice);

        var tenantId = await GetResolvedTenantAsync(client, ProbeUri);

        Assert.Equal(DevIdentities.Alice.TenantId, tenantId);
    }

    [Fact]
    public async Task ProtectedRoute_BobToken_ResolvesGlobex()
    {
        using var client = fixture.CreateClient(DevIdentities.Bob);

        var tenantId = await GetResolvedTenantAsync(client, ProbeUri);

        Assert.Equal(DevIdentities.Bob.TenantId, tenantId);
    }

    [Fact]
    public async Task ProtectedRoute_TenantIdInQueryOrHeader_IsIgnored()
    {
        var bobTenant = DevIdentities.Bob.TenantId.ToString();
        using var client = fixture.CreateClient(DevIdentities.Alice);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", bobTenant);
        client.DefaultRequestHeaders.Add("tenant_id", bobTenant);

        var tenantId = await GetResolvedTenantAsync(
            client, new Uri($"{TenantProbeStartupFilter.Path}?tenantId={bobTenant}&tenant_id={bobTenant}", UriKind.Relative));

        Assert.Equal(DevIdentities.Alice.TenantId, tenantId);
    }

    [Fact]
    public async Task ProtectedRoute_UnprovisionedIdentity_Returns403WithCode()
    {
        var stranger = new DevIdentity("mallory", Guid.NewGuid().ToString(), "Mallory", Guid.Empty, "None");
        using var client = fixture.CreateClient(stranger);

        using var response = await client.GetAsync(ProbeUri, TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Tenancy.User.NotProvisioned", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DevToken_SeededUser_ReturnsUsableToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var anonymous = fixture.Factory.CreateClient();

        using var tokenResponse = await anonymous.PostAsJsonAsync(DevTokenUri, new { user = "bob" }, cancellationToken);
        using var body = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("accessToken").GetString());

        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        Assert.True(body.RootElement.TryGetProperty("expiresAt", out _));
        Assert.Equal(DevIdentities.Bob.TenantId, await GetResolvedTenantAsync(client, ProbeUri));
    }

    [Fact]
    public async Task DevToken_UnknownUser_Returns400()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.PostAsJsonAsync(DevTokenUri, new { user = "mallory" }, TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("DevAuth.User.Unknown", problem.RootElement.GetProperty("code").GetString());
        Assert.False(problem.RootElement.TryGetProperty("accessToken", out _));
    }

    private static async Task<Guid> GetResolvedTenantAsync(HttpClient client, Uri uri)
    {
        using var response = await client.GetAsync(uri, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TenantProbeResponse>(TestContext.Current.CancellationToken);
        return body!.TenantId;
    }
}
