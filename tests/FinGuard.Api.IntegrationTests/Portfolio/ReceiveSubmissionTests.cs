using System.Diagnostics;
using System.Globalization;
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
    public async Task Upload_ValidCsv_Returns201AcceptedAndStoresPositions()
    {
        var content = Encoding.UTF8.GetBytes(
            "position_id,asset_class,book_value,currency,description\nP-1,cash,1000.00,USD,Bills\nP-2,Fixed_Income,250.5,usd,Bonds\n");

        using var response = await UploadAsync(DevIdentities.Alice, content, "portfolio.csv", Today);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(_ct);
        Assert.NotNull(body);
        Assert.Equal(("Accepted", 2, 0), (body.Status, body.RowCount, body.ErrorCount));
        Assert.Empty(body.Errors);
        Assert.Equal($"{Path}/{body.SubmissionId}", response.Headers.Location?.OriginalString);
        var row = await ReadRowAsync(body.SubmissionId);
        Assert.Equal(("Accepted", content.Length, Convert.ToHexStringLower(SHA256.HashData(content)), "portfolio.csv", new DateOnly(2026, 1, 1)),
            (row.Status, row.SizeBytes, row.Sha256, row.FileName, row.CutOffDate));
        Assert.Equal(content, row.Content);
        Assert.Equal(
            [(2, "P-1", "cash", 1000.00m), (3, "P-2", "fixed_income", 250.50m)],
            await ReadPositionsAsync(body.SubmissionId));
    }

    [Fact]
    public async Task Upload_InvalidCsv_Returns201RejectedAndStoresErrors()
    {
        var content = SubmissionTestData.InvalidCsv();

        using var response = await UploadAsync(DevIdentities.Alice, content, "invalid.csv", Today);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(_ct);
        Assert.NotNull(body);
        Assert.Equal(("Rejected", 2, 3), (body.Status, body.RowCount, body.ErrorCount));
        Assert.Equal(SubmissionTestData.InvalidCsvErrors, body.Errors.Select(e => (e.Row, e.Column, e.Code)));
        Assert.Equal($"{Path}/{body.SubmissionId}", response.Headers.Location?.OriginalString);
        var row = await ReadRowAsync(body.SubmissionId);
        Assert.Equal("Rejected", row.Status);
        Assert.Equal(content, row.Content);
        Assert.Empty(await ReadPositionsAsync(body.SubmissionId));
        Assert.Equal(
            SubmissionTestData.InvalidCsvErrors.Select((e, i) => (i, e.Row, e.Column, e.Code)),
            await ReadErrorsAsync(body.SubmissionId));
    }

    [Fact]
    public async Task Upload_FormulaInDescription_IsRejectedWithRowError()
    {
        var content = Csv("=HYPERLINK(1)");

        var body = await UploadBodyAsync(content);

        Assert.Equal(("Rejected", 1), (body.Status, body.ErrorCount));
        var error = Assert.Single(body.Errors);
        Assert.Equal((2, "description", "Portfolio.Csv.DescriptionUnsafe"), (error.Row, error.Column, error.Code));
        Assert.DoesNotContain("HYPERLINK", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await ReadPositionsAsync(body.SubmissionId));
    }

    [Fact]
    public async Task Upload_NonUsdCurrency_IsRejectedWithClearMessage()
    {
        var content = Encoding.UTF8.GetBytes(
            "position_id,asset_class,book_value,currency,description\nP-1,cash,1000.00,EUR,Bills\n");

        var body = await UploadBodyAsync(content);

        Assert.Equal("Rejected", body.Status);
        var error = Assert.Single(body.Errors);
        Assert.Equal(
            (2, "currency", "Portfolio.Csv.CurrencyNotSupported", "Only USD positions are supported in this version."),
            (error.Row, error.Column, error.Code, error.Message));
    }

    [Fact]
    public async Task Upload_TenThousandRows_IsAccepted()
    {
        var csv = new StringBuilder("position_id,asset_class,book_value,currency,description\n");
        for (var i = 1; i <= 10_000; i++)
        {
            csv.Append(CultureInfo.InvariantCulture, $"P-{i},shares,{i}.25,USD,Position {i}\n");
        }

        var stopwatch = Stopwatch.StartNew();
        using var response = await UploadAsync(DevIdentities.Alice, Encoding.UTF8.GetBytes(csv.ToString()), "big.csv", Today);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(_ct);
        Assert.Equal(("Accepted", 10_000), (body!.Status, body.RowCount));
        Assert.Equal(10_000L, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM portfolio.positions WHERE submission_id = @id", _ct, ("id", body.SubmissionId)));
        // R-30: reported, not asserted; a slow machine must not fail the build.
        TestContext.Current.TestOutputHelper?.WriteLine($"10,000-row upload took {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task Upload_ManyBadRows_StoresFirst100ErrorsAndTotalCount()
    {
        var csv = new StringBuilder("position_id,asset_class,book_value,currency,description\n");
        for (var i = 1; i <= 150; i++)
        {
            csv.Append(CultureInfo.InvariantCulture, $"P-{i},cash,1,EUR,\n");
        }

        using var response = await UploadAsync(DevIdentities.Alice, Encoding.UTF8.GetBytes(csv.ToString()), "bad.csv", Today);

        var body = await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(_ct);
        Assert.Equal(("Rejected", 150, 150, 100), (body!.Status, body.RowCount, body.ErrorCount, body.Errors.Count));
        Assert.Equal(150, (await ReadRowAsync(body.SubmissionId)).ErrorCount);
        var stored = await ReadErrorsAsync(body.SubmissionId);
        Assert.Equal(100, stored.Count);
        Assert.Equal((99, 101), (stored[^1].Ordinal, stored[^1].Row));
    }

    [Theory]
    [InlineData("empty", "portfolio.csv", Today)]
    [InlineData("csv", "portfolio.txt", Today)]
    [InlineData("csv", "portfolio.csv", "2999-01-01")]
    public async Task Upload_TransportFailure_StoresNothing(string body, string fileName, string cutOffDate)
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);

        using var response = await UploadAsync(identity, body == "empty" ? [] : Csv(), fileName, cutOffDate);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        foreach (var table in new[] { "submissions", "submission_contents", "positions", "submission_errors" })
        {
            Assert.Equal(0L, await fixture.ScalarAsync<long>(
                $"SELECT count(*) FROM portfolio.{table} WHERE tenant_id = @tenantId", _ct, ("tenantId", identity.TenantId)));
        }
    }

    [Fact]
    public async Task Upload_TwoFilesSameCutOffDate_BothStored()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);

        var first = await UploadOkAsync(identity, Csv("First version"));
        var second = await UploadOkAsync(identity, Csv("Second version"));

        Assert.NotEqual(first, second);
        Assert.Equal(2L, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM portfolio.submissions WHERE tenant_id = @tenantId AND cut_off_date = @date AND status = 'Accepted'",
            _ct, ("tenantId", identity.TenantId), ("date", new DateOnly(2026, 1, 1))));
    }

    [Fact]
    public async Task Upload_ValidCsv_StampsTenantOnPositionsAndErrors()
    {
        var accepted = await UploadOkAsync(DevIdentities.Alice);
        var rejected = await UploadOkAsync(DevIdentities.Alice, SubmissionTestData.InvalidCsv());

        var positionTenants = await fixture.ScalarAsync<Guid[]>(
            "SELECT array_agg(DISTINCT tenant_id) FROM portfolio.positions WHERE submission_id = @id", _ct, ("id", accepted));
        var errorTenants = await fixture.ScalarAsync<Guid[]>(
            "SELECT array_agg(DISTINCT tenant_id) FROM portfolio.submission_errors WHERE submission_id = @id", _ct, ("id", rejected));

        Assert.Equal([DevIdentities.Alice.TenantId], positionTenants);
        Assert.Equal([DevIdentities.Alice.TenantId], errorTenants);
    }

    [Fact]
    public async Task Upload_AsBob_PositionsAndErrorsAreInvisibleToAlicesContext()
    {
        var accepted = await UploadOkAsync(DevIdentities.Bob);
        var rejected = await UploadOkAsync(DevIdentities.Bob, SubmissionTestData.InvalidCsv());
        await using var alice = CreateContext(DevIdentities.Alice.TenantId);

        Assert.False(await alice.Set<Position>().AnyAsync(p => p.SubmissionId == accepted, _ct));
        Assert.False(await alice.Set<SubmissionError>().AnyAsync(e => e.SubmissionId == rejected, _ct));
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
    public async Task Upload_FutureCutOffDate_ProblemCarriesDetailAndCode()
    {
        using var response = await UploadAsync(DevIdentities.Alice, Csv(), "portfolio.csv", "2999-01-01");

        // The SPA shows title and detail for 4xx (D-142, R-28).
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            ("Validation failed", "The cut-off date must not be later than today (UTC).", "Portfolio.Submission.CutOffDateInFuture"),
            (problem.RootElement.GetProperty("title").GetString(), problem.RootElement.GetProperty("detail").GetString(),
             problem.RootElement.GetProperty("code").GetString()));
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
    public async Task Upload_Any_DoesNotLogContentNamesAmountsOrDescriptions()
    {
        var marker = $"marker{Guid.NewGuid():N}";
        var amount = "73519.47";
        var valid = Encoding.UTF8.GetBytes(
            $"position_id,asset_class,book_value,currency,description\nID{marker},cash,{amount},USD,Desc{marker}\n");
        var invalid = Encoding.UTF8.GetBytes(
            $"position_id,asset_class,book_value,currency,description\nID{marker},Class{marker},-{amount},Ccy{marker},=Desc{marker}\n");
        var hash = Convert.ToHexStringLower(SHA256.HashData(valid));

        var acceptedId = await UploadOkAsync(DevIdentities.Alice, valid, $"{marker}.csv");
        var rejectedId = await UploadOkAsync(DevIdentities.Alice, invalid, $"{marker}.csv");
        using var transport = await UploadAsync(DevIdentities.Alice, valid, $"{marker}.txt", Today);

        Assert.Equal(HttpStatusCode.BadRequest, transport.StatusCode);
        var records = fixture.Logs.GetSnapshot();
        // One line per stored upload: id, size, status and counts.
        Assert.Single(records, r => r.Message.Contains(acceptedId.ToString(), StringComparison.Ordinal)
                                    && r.Message.Contains(": Accepted, 1 rows, 0 errors", StringComparison.Ordinal));
        Assert.Single(records, r => r.Message.Contains(rejectedId.ToString(), StringComparison.Ordinal)
                                    && r.Message.Contains(": Rejected, 1 rows, 4 errors", StringComparison.Ordinal));
        var leaks = records
            .Where(r => new[] { marker, hash, amount }.Any(secret =>
                r.Message.Contains(secret, StringComparison.OrdinalIgnoreCase)
                || r.StructuredState?.Any(kv => kv.Value?.Contains(secret, StringComparison.OrdinalIgnoreCase) == true) == true
                || r.Exception?.ToString().Contains(secret, StringComparison.OrdinalIgnoreCase) == true))
            .Select(r => $"[{r.Level}] {r.Category}: {r.Message}")
            .ToList();
        Assert.True(leaks.Count == 0, $"Log records leak upload data: {string.Join(" | ", leaks)}");
    }

    private async Task<SubmissionReceivedResponse> UploadBodyAsync(byte[] content)
    {
        using var response = await UploadAsync(DevIdentities.Alice, content, "portfolio.csv", Today);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(_ct);
        Assert.NotNull(body);
        return body;
    }

    // Unique per call (D-139): the same bytes and cut-off date would return the earlier submission.
    private static byte[] Csv(string description = "Treasury bills") => Encoding.UTF8.GetBytes(
        $"position_id,asset_class,book_value,currency,description\n{SubmissionTestData.UniquePositionId()},cash,1000.00,USD,{description}\n");

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
            SELECT s.tenant_id, s.cut_off_date, s.file_name, s.size_bytes, s.sha256, s.status, c.tenant_id, c.content, s.error_count
            FROM portfolio.submissions s JOIN portfolio.submission_contents c ON c.submission_id = s.id
            WHERE s.id = @id
            """, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(_ct);
        Assert.True(await reader.ReadAsync(_ct), $"No stored submission {id}");
        return new StoredRow(reader.GetGuid(0), reader.GetFieldValue<DateOnly>(1), reader.GetString(2), reader.GetInt32(3),
            reader.GetString(4), reader.GetString(5), reader.GetGuid(6), reader.GetFieldValue<byte[]>(7), reader.GetInt32(8));
    }

    private async Task<List<(int Row, string PositionId, string AssetClass, decimal BookValue)>> ReadPositionsAsync(Guid id)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(_ct);
        await using var command = new NpgsqlCommand(
            "SELECT row_number, position_id, asset_class, book_value FROM portfolio.positions WHERE submission_id = @id ORDER BY row_number",
            connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(_ct);
        var rows = new List<(int, string, string, decimal)>();
        while (await reader.ReadAsync(_ct))
        {
            rows.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3)));
        }

        return rows;
    }

    private async Task<List<(int Ordinal, int? Row, string? Column, string Code)>> ReadErrorsAsync(Guid id)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(_ct);
        await using var command = new NpgsqlCommand(
            """SELECT ordinal, row_number, "column", code FROM portfolio.submission_errors WHERE submission_id = @id ORDER BY ordinal""",
            connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(_ct);
        var rows = new List<(int, int?, string?, string)>();
        while (await reader.ReadAsync(_ct))
        {
            rows.Add((reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3)));
        }

        return rows;
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
        Guid ContentTenantId, byte[] Content, int ErrorCount);
}
