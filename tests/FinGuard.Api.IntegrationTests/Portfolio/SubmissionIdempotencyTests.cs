using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinGuard.Portfolio.Features.ReceiveSubmission;

namespace FinGuard.Api.IntegrationTests.Portfolio;

// Brief #11 (D-139, D-140): same tenant + cut-off date + SHA-256 returns the earliest submission; nothing is stored.
public sealed class SubmissionIdempotencyTests(ApiFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_SameFileSameDateTwice_Returns200DuplicateWithSameIdAndStoresOneRow()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        using var client = fixture.CreateClient(identity);
        var content = SubmissionTestData.Csv();
        var first = await client.UploadOkAsync("portfolio.csv", _ct, content);

        using var response = await client.UploadAsync("portfolio.csv", _ct, content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"/api/portfolio/submissions/{first.SubmissionId}", response.Headers.Location?.OriginalString);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        Assert.True(json.RootElement.GetProperty("duplicate").GetBoolean());
        Assert.Equal(first.SubmissionId, json.RootElement.GetProperty("submissionId").GetGuid());
        Assert.Equal("Accepted", json.RootElement.GetProperty("status").GetString());
        Assert.False(first.Duplicate);
        Assert.Equal(1L, await CountAsync("submissions", identity.TenantId));
        Assert.Equal(1L, await CountAsync("submission_contents", identity.TenantId));
        Assert.Equal(1L, await CountAsync("positions", identity.TenantId));
    }

    [Fact]
    public async Task Upload_SameFileDifferentDate_Returns201New()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var content = SubmissionTestData.Csv();
        var first = await client.UploadOkAsync("portfolio.csv", _ct, content, "2026-01-01");

        var second = await client.UploadOkAsync("portfolio.csv", _ct, content, "2026-01-02");

        Assert.False(second.Duplicate);
        Assert.NotEqual(first.SubmissionId, second.SubmissionId);
    }

    [Fact]
    public async Task Upload_SameFileAsOtherTenant_Returns201New()
    {
        var content = SubmissionTestData.Csv();
        using var tenantA = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        using var tenantB = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var first = await tenantA.UploadOkAsync("portfolio.csv", _ct, content);

        var second = await tenantB.UploadOkAsync("portfolio.csv", _ct, content);

        Assert.False(second.Duplicate);
        Assert.NotEqual(first.SubmissionId, second.SubmissionId);
    }

    [Fact]
    public async Task Upload_DuplicateOfRejected_ReturnsRejectedWithErrors()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var content = SubmissionTestData.InvalidCsv();
        var first = await client.UploadOkAsync("invalid.csv", _ct, content);

        var duplicate = await UploadDuplicateAsync(client, "invalid.csv", content);

        Assert.Equal((first.SubmissionId, "Rejected", 3), (duplicate.SubmissionId, duplicate.Status, duplicate.ErrorCount));
        Assert.Equal(
            SubmissionTestData.InvalidCsvErrors,
            duplicate.Errors.Select(e => (e.Row, e.Column, e.Code)));
    }

    [Fact]
    public async Task Upload_DuplicateWithDifferentFileName_IsDuplicate()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var content = SubmissionTestData.Csv();
        var first = await client.UploadOkAsync("january.csv", _ct, content);

        var duplicate = await UploadDuplicateAsync(client, "renamed.csv", content);

        Assert.Equal(first.SubmissionId, duplicate.SubmissionId);
    }

    [Fact]
    public async Task Upload_Duplicate_LogsIdsOnly()
    {
        var marker = $"marker{Guid.NewGuid():N}";
        var content = Encoding.UTF8.GetBytes(
            $"position_id,asset_class,book_value,currency,description\nID{marker},cash,1,USD,Desc{marker}\n");
        var hash = Convert.ToHexStringLower(SHA256.HashData(content));
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var first = await client.UploadOkAsync($"{marker}.csv", _ct, content);

        await UploadDuplicateAsync(client, $"{marker}-copy.csv", content);

        var records = fixture.Logs.GetSnapshot();
        Assert.Single(records, r => r.Message == $"Duplicate upload of submission {first.SubmissionId}");
        var leaks = records
            .Where(r => new[] { marker, hash }.Any(secret =>
                r.Message.Contains(secret, StringComparison.OrdinalIgnoreCase)
                || r.StructuredState?.Any(kv => kv.Value?.Contains(secret, StringComparison.OrdinalIgnoreCase) == true) == true))
            .Select(r => $"[{r.Level}] {r.Category}: {r.Message}")
            .ToList();
        Assert.True(leaks.Count == 0, $"Log records leak upload data: {string.Join(" | ", leaks)}");
    }

    private async Task<SubmissionReceivedResponse> UploadDuplicateAsync(HttpClient client, string fileName, byte[] content)
    {
        using var response = await client.UploadAsync(fileName, _ct, content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(_ct))!;
        Assert.True(body.Duplicate);
        return body;
    }

    private Task<long> CountAsync(string table, Guid tenantId) =>
        fixture.ScalarAsync<long>(
            $"SELECT count(*) FROM portfolio.{table} WHERE tenant_id = @tenantId", _ct, ("tenantId", tenantId));
}
