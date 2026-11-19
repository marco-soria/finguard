using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinGuard.Api.DevAuth;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Portfolio.Features.ReceiveSubmission;
using FinGuard.Portfolio.Persistence;
using FinGuard.Tenancy.Development;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FinGuard.Api.IntegrationTests.Portfolio;

// First write slice (Brief #8): multipart form -> Wolverine command handler -> EF Core, tenant from ITenantContext.
public sealed class ReceiveSubmissionTests(ApiFixture fixture)
{
    private const string Path = "/api/portfolio/submissions";
    private const string Today = "2026-01-01";

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_ValidCsv_Returns201AndStoresSubmissionAndContent()
    {
        var content = Csv();

        using var response = await UploadAsync(DevIdentities.Alice, content, "portfolio.csv", Today);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(_ct);
        Assert.NotNull(body);
        Assert.Equal("Received", body.Status);
        Assert.Equal($"{Path}/{body.SubmissionId}", response.Headers.Location?.OriginalString);
        var row = await ReadRowAsync(body.SubmissionId);
        Assert.Equal(("Received", content.Length, Convert.ToHexStringLower(SHA256.HashData(content)), "portfolio.csv", new DateOnly(2026, 1, 1)),
            (row.Status, row.SizeBytes, row.Sha256, row.FileName, row.CutOffDate));
        Assert.Equal(content, row.Content);
    }

    [Fact]
    public async Task Upload_ValidCsv_StampsCurrentTenant()
    {
        var id = await UploadOkAsync(DevIdentities.Alice);

        var row = await ReadRowAsync(id);

        Assert.Equal((DevIdentities.Alice.TenantId, DevIdentities.Alice.TenantId), (row.TenantId, row.ContentTenantId));
    }

    [Fact]
    public async Task Upload_TenantIdInFormField_IsIgnored()
    {
        // AGENTS.md rule 7: the tenant never comes from the request.
        using var form = Form(Csv(), "portfolio.csv", Today);
        form.Add(new StringContent(DevIdentities.Bob.TenantId.ToString()), "tenantId");
        using var client = fixture.CreateClient(DevIdentities.Alice);

        using var response = await client.PostAsync(new Uri(Path, UriKind.Relative), form, _ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(_ct);
        Assert.Equal(DevIdentities.Alice.TenantId, (await ReadRowAsync(body!.SubmissionId)).TenantId);
    }

    [Fact]
    public async Task Upload_AsBob_IsInvisibleToAlicesContext()
    {
        var bobsId = await UploadOkAsync(DevIdentities.Bob);
        await using var alice = CreateContext(DevIdentities.Alice.TenantId);
        await using var bob = CreateContext(DevIdentities.Bob.TenantId);

        var seenByAlice = await alice.Set<Submission>().AnyAsync(s => s.Id == bobsId, _ct)
                          || await alice.Set<SubmissionContent>().AnyAsync(c => c.SubmissionId == bobsId, _ct);
        var seenByBob = await bob.Set<Submission>().AnyAsync(s => s.Id == bobsId, _ct)
                        && await bob.Set<SubmissionContent>().AnyAsync(c => c.SubmissionId == bobsId, _ct);

        Assert.False(seenByAlice);
        Assert.True(seenByBob);
    }

    [Theory]
    [InlineData("empty", "portfolio.csv", Today, "Portfolio.Submission.FileEmpty")]
    [InlineData("csv", "portfolio.txt", Today, "Portfolio.Submission.NotCsv")]
    [InlineData("csv", "portfolio.csv", "01/02/2026", "Portfolio.Submission.CutOffDateInvalid")]
    [InlineData("csv", "portfolio.csv", "", "Portfolio.Submission.CutOffDateInvalid")]
    [InlineData("csv", "portfolio.csv", "2999-01-01", "Portfolio.Submission.CutOffDateInFuture")]
    public async Task Upload_InvalidInput_Returns400WithCode(string body, string fileName, string cutOffDate, string code)
    {
        var content = body == "empty" ? [] : Csv();

        using var response = await UploadAsync(DevIdentities.Alice, content, fileName, cutOffDate);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Upload_MissingFile_Returns400WithCode()
    {
        using var form = new MultipartFormDataContent { { new StringContent(Today), "cutOffDate" } };
        using var client = fixture.CreateClient(DevIdentities.Alice);

        using var response = await client.PostAsync(new Uri(Path, UriKind.Relative), form, _ct);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Portfolio.Submission.FileMissing");
    }

    [Fact]
    public async Task Upload_FileOverFiveMegabytes_Returns400WithCode()
    {
        // Under the 6 MB request limit, over the 5 MB file limit: the handler rejects it.
        using var response = await UploadAsync(DevIdentities.Alice, new byte[(5 * 1024 * 1024) + 1], "big.csv", Today);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Portfolio.Submission.FileTooLarge");
    }

    [Fact]
    public async Task Upload_BodyOverLimit_Returns413Problem()
    {
        // TestServer ignores IRequestSizeLimitMetadata (no IHttpMaxRequestBodySizeFeature), so this test boots the Api
        // on real Kestrel against the same database. Tokens come from that instance's own development issuer.
        await using var kestrel = new ApiFixture.ApiFactory(fixture.ConnectionString);
        kestrel.UseKestrel(0);
        kestrel.StartServer();
        var token = kestrel.Services.GetRequiredService<DevTokenIssuer>()
            .CreateToken(DevIdentities.Alice, TimeProvider.System.GetUtcNow());
        using var client = kestrel.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        // Like curl for large bodies: Kestrel answers 413 from Content-Length instead of 100 Continue, so the client
        // never sends the body. Without it Kestrel closes the connection while the client is still writing.
        client.DefaultRequestHeaders.ExpectContinue = true;
        using var form = Form(new byte[7 * 1024 * 1024], "huge.csv", Today);

        using var response = await client.PostAsync(new Uri(Path, UriKind.Relative), form, _ct);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        Assert.Equal(413, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Upload_NoToken_Returns401()
    {
        using var form = Form(Csv(), "portfolio.csv", Today);
        using var client = fixture.Factory.CreateClient();

        using var response = await client.PostAsync(new Uri(Path, UriKind.Relative), form, _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_Any_DoesNotLogContentFileNameOrHash()
    {
        var marker = $"marker{Guid.NewGuid():N}";
        var content = Csv(marker);
        var hash = Convert.ToHexStringLower(SHA256.HashData(content));

        var id = await UploadOkAsync(DevIdentities.Alice, content, $"{marker}.csv");
        using var rejected = await UploadAsync(DevIdentities.Alice, content, $"{marker}.txt", Today);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var records = fixture.Logs.GetSnapshot();
        Assert.Contains(records, r => r.Message.Contains(id.ToString(), StringComparison.Ordinal));
        var leaks = records
            .Where(r => new[] { marker, hash }.Any(secret =>
                r.Message.Contains(secret, StringComparison.OrdinalIgnoreCase)
                || r.StructuredState?.Any(kv => kv.Value?.Contains(secret, StringComparison.OrdinalIgnoreCase) == true) == true
                || r.Exception?.ToString().Contains(secret, StringComparison.OrdinalIgnoreCase) == true))
            .Select(r => $"[{r.Level}] {r.Category}: {r.Message}")
            .ToList();
        Assert.True(leaks.Count == 0, $"Log records leak upload data: {string.Join(" | ", leaks)}");
    }

    private static byte[] Csv(string description = "Treasury bills") => Encoding.UTF8.GetBytes(
        $"position_id,asset_class,market_value_usd,currency,description\nP-1,cash,1000.00,USD,{description}\n");

    private static MultipartFormDataContent Form(byte[] content, string fileName, string cutOffDate)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { file, "file", fileName }, { new StringContent(cutOffDate), "cutOffDate" } };
    }

    private async Task<HttpResponseMessage> UploadAsync(DevIdentity identity, byte[] content, string fileName, string cutOffDate)
    {
        using var form = Form(content, fileName, cutOffDate);
        using var client = fixture.CreateClient(identity);
        return await client.PostAsync(new Uri(Path, UriKind.Relative), form, _ct);
    }

    private async Task<Guid> UploadOkAsync(DevIdentity identity, byte[]? content = null, string fileName = "portfolio.csv")
    {
        using var response = await UploadAsync(identity, content ?? Csv(), fileName, Today);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(_ct))!.SubmissionId;
    }

    private async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
    }

    // Raw SQL as the database owner: no query filter, so the test sees exactly what was written.
    private async Task<StoredRow> ReadRowAsync(Guid id)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(_ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT s.tenant_id, s.cut_off_date, s.file_name, s.size_bytes, s.sha256, s.status, c.tenant_id, c.content
            FROM portfolio.submissions s JOIN portfolio.submission_contents c ON c.submission_id = s.id
            WHERE s.id = @id
            """, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(_ct);
        Assert.True(await reader.ReadAsync(_ct), $"No stored submission {id}");
        return new StoredRow(reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.GetString(2), reader.GetInt32(3),
            reader.GetString(4), reader.GetString(5), reader.GetGuid(6), reader.GetFieldValue<byte[]>(7));
    }

    private PortfolioDbContext CreateContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var options = new DbContextOptionsBuilder<PortfolioDbContext>();
        options.UseModuleNpgsql(fixture.ConnectionString, PortfolioDbContext.Schema);
        return new PortfolioDbContext(options.Options, tenantContext);
    }

    private sealed record StoredRow(
        Guid TenantId, DateOnly CutOffDate, string FileName, int SizeBytes, string Sha256, string Status,
        Guid ContentTenantId, byte[] Content);
}
