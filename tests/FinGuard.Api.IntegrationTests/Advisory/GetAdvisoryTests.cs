using System.Net;
using System.Net.Http.Json;
using FinGuard.Advisory.Features.GetAdvisory;
using FinGuard.Api.IntegrationTests.Portfolio;
using FinGuard.Tenancy.Development;

namespace FinGuard.Api.IntegrationTests.Advisory;

/// <summary>Brief #18 (D-256): <c>GET /api/advisory/submissions/{id}/advisory</c>, Dapper with the tenant filter.</summary>
[Collection(SubmissionEvents.Collection)]
public sealed class GetAdvisoryTests(ApiFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAdvisory_SharesHeavy_ReturnsTheGoldenParagraphs()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        using var client = fixture.CreateClient(identity);
        var submissionId = await UploadAsync(client, "valid-shares-heavy.csv");

        var advisory = await client.GetFromJsonAsync<AdvisoryResponse>(PathFor(submissionId), _ct);

        Assert.NotNull(advisory);
        Assert.Equal((submissionId, "Template"), (advisory.SubmissionId, advisory.Narrator));
        Assert.Equal(AdvisoryTestData.SharesHeavyNarrative.Split("\n\n"), advisory.Paragraphs);
    }

    [Fact]
    public async Task Upload_NoModelConfigured_RefinementNotRequested()
    {
        // The shared host has no narrator connection string, like CI and a machine without Ollama (ADR-0015).
        var identity = await fixture.ProvisionTenantAsync(_ct);
        using var client = fixture.CreateClient(identity);
        var submissionId = await UploadAsync(client, "valid-shares-heavy.csv");

        var advisory = await client.GetFromJsonAsync<AdvisoryResponse>(PathFor(submissionId), _ct);

        Assert.NotNull(advisory);
        Assert.Equal(("Template", (string?)null, "NotRequested"), (advisory.Narrator, advisory.Model, advisory.Refinement));
    }

    [Fact]
    public async Task GetAdvisory_OtherTenant_ReturnsNotFound()
    {
        var owner = await fixture.ProvisionTenantAsync(_ct);
        using var ownerClient = fixture.CreateClient(owner);
        var submissionId = await UploadAsync(ownerClient, "valid-shares-heavy.csv");
        using var alice = fixture.CreateClient(DevIdentities.Alice);

        await AssertNotFoundAsync(alice, submissionId);
        using var own = await ownerClient.GetAsync(PathFor(submissionId), _ct);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task GetAdvisory_Unknown_ReturnsNotFound()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice);

        await AssertNotFoundAsync(client, Guid.NewGuid());
    }

    private async Task AssertNotFoundAsync(HttpClient client, Guid submissionId)
    {
        using var response = await client.GetAsync(PathFor(submissionId), _ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Advisory.Advisory.NotFound", await response.Content.ReadAsStringAsync(_ct), StringComparison.Ordinal);
    }

    private static string PathFor(Guid submissionId) => $"/api/advisory/submissions/{submissionId}/advisory";

    // Callers use a freshly provisioned tenant, so the sample is never a duplicate upload (D-139).
    private async Task<Guid> UploadAsync(HttpClient client, string sample)
    {
        var content = AdvisoryTestData.Sample(sample);
        var (_, response) = await SubmissionEvents.UploadTrackedAsync(
            fixture, client, content, "2026-06-30", HttpStatusCode.Created, _ct);
        return response.SubmissionId;
    }
}
