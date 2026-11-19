using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinGuard.Portfolio.Features.GetSubmission;
using FinGuard.Tenancy.Development;

namespace FinGuard.Api.IntegrationTests.Portfolio;

// Details read slice (Brief #9): another tenant's id is indistinguishable from an unknown one (D-101).
public sealed class GetSubmissionTests(ApiFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetSubmission_OwnSubmission_Returns200WithFields()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice);
        var received = await client.UploadOkAsync("details.csv", _ct);

        using var response = await client.GetAsync(DetailsUri(received.SubmissionId), _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubmissionDetailsResponse>(_ct);
        Assert.NotNull(body);
        Assert.Equal(
            (received.SubmissionId, new DateOnly(2026, 1, 1), "details.csv", SubmissionTestData.Csv().Length, "Accepted", 1, 0),
            (body.SubmissionId, body.CutOffDate, body.FileName, body.SizeBytes, body.Status, body.RowCount, body.ErrorCount));
        Assert.Empty(body.Errors);
        // PostgreSQL stores microseconds; the upload response still carries .NET ticks (100 ns).
        Assert.True((body.ReceivedAt - received.ReceivedAt).Duration() < TimeSpan.FromMicroseconds(1));
    }

    [Fact]
    public async Task GetSubmission_OtherTenantsSubmission_Returns404WithCode()
    {
        using var bob = fixture.CreateClient(DevIdentities.Bob);
        var bobsId = (await bob.UploadOkAsync("bob.csv", _ct)).SubmissionId;
        using var alice = fixture.CreateClient(DevIdentities.Alice);

        using var response = await alice.GetAsync(DetailsUri(bobsId), _ct);

        await AssertNotFoundAsync(response);
    }

    [Fact]
    public async Task GetSubmission_Rejected_ReturnsErrors()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice);
        var received = await client.UploadOkAsync("rejected.csv", _ct, SubmissionTestData.InvalidCsv());

        using var response = await client.GetAsync(DetailsUri(received.SubmissionId), _ct);

        var body = await response.Content.ReadFromJsonAsync<SubmissionDetailsResponse>(_ct);
        Assert.NotNull(body);
        Assert.Equal(("Rejected", 2, 3), (body.Status, body.RowCount, body.ErrorCount));
        Assert.Equal(SubmissionTestData.InvalidCsvErrors, body.Errors.Select(e => (e.Row, e.Column, e.Code)));
        Assert.All(body.Errors, e => Assert.False(string.IsNullOrEmpty(e.Message)));
    }

    [Fact]
    public async Task GetSubmission_OtherTenantsRejected_Returns404()
    {
        using var bob = fixture.CreateClient(DevIdentities.Bob);
        var bobsId = (await bob.UploadOkAsync("bob-rejected.csv", _ct, SubmissionTestData.InvalidCsv())).SubmissionId;
        using var alice = fixture.CreateClient(DevIdentities.Alice);

        using var response = await alice.GetAsync(DetailsUri(bobsId), _ct);

        await AssertNotFoundAsync(response);
    }

    [Fact]
    public async Task GetSubmission_UnknownId_Returns404WithCode()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice);

        using var response = await client.GetAsync(DetailsUri(Guid.NewGuid()), _ct);

        await AssertNotFoundAsync(response);
    }

    [Fact]
    public async Task GetSubmission_NotAGuid_Returns404()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice);

        using var response = await client.GetAsync(new Uri($"{SubmissionTestData.Path}/not-a-guid", UriKind.Relative), _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_LocationHeader_ResolvesToSubmission()
    {
        using var client = fixture.CreateClient(DevIdentities.Alice);
        using var upload = await client.UploadAsync("location.csv", _ct);
        Assert.NotNull(upload.Headers.Location);

        using var response = await client.GetAsync(upload.Headers.Location, _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubmissionDetailsResponse>(_ct);
        Assert.Equal("location.csv", body?.FileName);
    }

    [Fact]
    public async Task ReadSlices_Always_DoNotLogFileNames()
    {
        var marker = $"marker{Guid.NewGuid():N}";
        using var client = fixture.CreateClient(DevIdentities.Alice);
        var id = (await client.UploadOkAsync($"{marker}.csv", _ct)).SubmissionId;

        using var list = await client.GetAsync(new Uri(SubmissionTestData.Path, UriKind.Relative), _ct);
        using var details = await client.GetAsync(DetailsUri(id), _ct);

        // The reads did return the name; it must not reach the logs.
        Assert.Contains(marker, await list.Content.ReadAsStringAsync(_ct), StringComparison.Ordinal);
        Assert.Contains(marker, await details.Content.ReadAsStringAsync(_ct), StringComparison.Ordinal);
        var leaks = fixture.Logs.GetSnapshot()
            .Where(r => r.Message.Contains(marker, StringComparison.OrdinalIgnoreCase)
                        || r.StructuredState?.Any(kv => kv.Value?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true) == true
                        || r.Exception?.ToString().Contains(marker, StringComparison.OrdinalIgnoreCase) == true)
            .Select(r => $"[{r.Level}] {r.Category}: {r.Message}")
            .ToList();
        Assert.True(leaks.Count == 0, $"Log records leak file names: {string.Join(" | ", leaks)}");
    }

    private static Uri DetailsUri(Guid id) => new($"{SubmissionTestData.Path}/{id}", UriKind.Relative);

    private async Task AssertNotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));
        Assert.Equal("Portfolio.Submission.NotFound", problem.RootElement.GetProperty("code").GetString());
    }
}
