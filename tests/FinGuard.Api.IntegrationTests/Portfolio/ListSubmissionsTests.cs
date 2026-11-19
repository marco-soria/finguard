using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinGuard.Portfolio.Features.ListSubmissions;
using FinGuard.Portfolio.Persistence;
using FinGuard.Tenancy.Development;

namespace FinGuard.Api.IntegrationTests.Portfolio;

// First Portfolio read slice (Brief #9): Dapper, own schema, tenant filter, named cap, deterministic order (D-100).
public sealed class ListSubmissionsTests(ApiFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListSubmissions_NoSubmissions_ReturnsEmptyItems()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));

        var body = await ListAsync(client);

        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task ListSubmissions_TwoUploads_ReturnsNewestFirst()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var first = await client.UploadOkAsync("first.csv", _ct);
        var second = await client.UploadOkAsync("second.csv", _ct);

        var body = await ListAsync(client);

        Assert.Equal([second.SubmissionId, first.SubmissionId], body.Items.Select(i => i.SubmissionId));
    }

    [Fact]
    public async Task ListSubmissions_AliceAndBob_EachSeesOnlyOwn()
    {
        using var alice = fixture.CreateClient(DevIdentities.Alice);
        using var bob = fixture.CreateClient(DevIdentities.Bob);
        var alicesId = (await alice.UploadOkAsync("alice.csv", _ct)).SubmissionId;
        var bobsId = (await bob.UploadOkAsync("bob.csv", _ct)).SubmissionId;

        var seenByAlice = (await ListAsync(alice)).Items.Select(i => i.SubmissionId).ToList();
        var seenByBob = (await ListAsync(bob)).Items.Select(i => i.SubmissionId).ToList();

        Assert.Contains(alicesId, seenByAlice);
        Assert.DoesNotContain(bobsId, seenByAlice);
        Assert.Contains(bobsId, seenByBob);
        Assert.DoesNotContain(alicesId, seenByBob);
    }

    [Fact]
    public async Task ListSubmissions_MoreThanCap_ReturnsNewest100()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var seeded = Enumerable.Range(0, ListSubmissionsHandler.Max + 1)
            .Select(i => Submission.Receive(Guid.NewGuid(), new DateOnly(2026, 1, 1), $"seed-{i}.csv", 10,
                new string('a', Submission.Sha256Length), start.AddMinutes(i)))
            .ToList();
        // Seeded through the tenant's own context: the stamping interceptor and query filter stay in force.
        await using (var context = fixture.CreatePortfolioContext(identity.TenantId))
        {
            context.AddRange(seeded);
            await context.SaveChangesAsync(_ct);
        }

        using var client = fixture.CreateClient(identity);
        var body = await ListAsync(client);

        var expected = seeded.OrderByDescending(s => s.ReceivedAt).Take(ListSubmissionsHandler.Max).Select(s => s.Id);
        Assert.Equal(expected, body.Items.Select(i => i.SubmissionId));
    }

    [Fact]
    public async Task ListSubmissions_Always_ReturnsOnlyPublicFields()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        await client.UploadOkAsync("fields.csv", _ct);

        using var response = await client.GetAsync(new Uri(SubmissionTestData.Path, UriKind.Relative), _ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));

        Assert.Equal(["items"], json.RootElement.EnumerateObject().Select(p => p.Name));
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal(
            ["cutOffDate", "fileName", "receivedAt", "sizeBytes", "status", "submissionId"],
            item.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        // A calendar date on the wire, never a timestamp (D-103).
        Assert.Equal(SubmissionTestData.CutOffDate, item.GetProperty("cutOffDate").GetString());
    }

    [Fact]
    public async Task ListSubmissions_NoToken_Returns401()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(new Uri(SubmissionTestData.Path, UriKind.Relative), _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<SubmissionListResponse> ListAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri(SubmissionTestData.Path, UriKind.Relative), _ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SubmissionListResponse>(_ct))!;
    }
}
