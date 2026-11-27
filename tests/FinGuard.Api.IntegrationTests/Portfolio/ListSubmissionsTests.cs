using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinGuard.Portfolio.Features.ListSubmissions;
using FinGuard.Portfolio.Persistence;
using FinGuard.Tenancy.Development;

namespace FinGuard.Api.IntegrationTests.Portfolio;

// Portfolio history (Brief #9, Brief #20): Dapper, own schema, tenant filter, keyset paging and filters (D-299, D-304).
public sealed class ListSubmissionsTests(ApiFixture fixture)
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListSubmissions_NoSubmissions_ReturnsEmptyItemsAndNoCursor()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));

        var body = await ListAsync(client);

        Assert.Empty(body.Items);
        Assert.Null(body.NextCursor);
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

        var seenByAlice = (await ListAsync(alice, "limit=100")).Items.Select(i => i.SubmissionId).ToList();
        var seenByBob = (await ListAsync(bob, "limit=100")).Items.Select(i => i.SubmissionId).ToList();

        Assert.Contains(alicesId, seenByAlice);
        Assert.DoesNotContain(bobsId, seenByAlice);
        Assert.Contains(bobsId, seenByBob);
        Assert.DoesNotContain(alicesId, seenByBob);
    }

    [Fact]
    public async Task ListSubmissions_NoLimit_ReturnsDefaultPageOf25()
    {
        var (client, seeded) = await SeedTenantAsync(26);

        var body = await ListAsync(client);

        Assert.Equal(Newest(seeded).Take(25), body.Items.Select(i => i.SubmissionId));
        Assert.NotNull(body.NextCursor);
    }

    [Fact]
    public async Task ListSubmissions_MoreThanMax_ReturnsNewest100WithCursor()
    {
        var (client, seeded) = await SeedTenantAsync(ListSubmissionsHandler.Max + 1);

        var body = await ListAsync(client, $"limit={ListSubmissionsHandler.Max}");

        Assert.Equal(Newest(seeded).Take(ListSubmissionsHandler.Max), body.Items.Select(i => i.SubmissionId));
        Assert.NotNull(body.NextCursor);
    }

    [Fact]
    public async Task List_PagesThroughAllSubmissions_WithoutGapsOrDuplicates()
    {
        // Pairs share a received_at, so the id tiebreaker decides the order inside each pair.
        var (client, seeded) = await SeedTenantAsync(60, i => Start.AddMinutes(i / 2));

        var pages = await ReadAllPagesAsync(client, "limit=25");

        Assert.Equal([25, 25, 10], pages.Select(p => p.Items.Count));
        Assert.Null(pages[^1].NextCursor);
        Assert.Equal(Newest(seeded), pages.SelectMany(p => p.Items).Select(i => i.SubmissionId));
    }

    [Fact]
    public async Task List_NewUploadBetweenPages_DoesNotShiftLaterPages()
    {
        var (client, seeded) = await SeedTenantAsync(30);
        var first = await ListAsync(client, "limit=10");

        await client.UploadOkAsync("between-pages.csv", _ct);
        var second = await ListAsync(client, $"limit=10&cursor={first.NextCursor}");

        Assert.Equal(Newest(seeded).Skip(10).Take(10), second.Items.Select(i => i.SubmissionId));
    }

    [Fact]
    public async Task List_FilterByStatus()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        var accepted = Accept(new DateOnly(2026, 1, 1), Start);
        var rejected = Reject(new DateOnly(2026, 1, 1), Start.AddMinutes(1));
        await SeedAsync(identity, accepted, rejected);
        using var client = fixture.CreateClient(identity);

        var onlyRejected = await ListAsync(client, "status=Rejected");
        var onlyAccepted = await ListAsync(client, "status=accepted");

        Assert.Equal([rejected.Id], onlyRejected.Items.Select(i => i.SubmissionId));
        Assert.Equal([accepted.Id], onlyAccepted.Items.Select(i => i.SubmissionId));
    }

    [Fact]
    public async Task List_FilterByCutOffRange()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        var before = Accept(new DateOnly(2025, 12, 31), Start);
        var from = Accept(new DateOnly(2026, 1, 1), Start.AddMinutes(1));
        var to = Accept(new DateOnly(2026, 1, 31), Start.AddMinutes(2));
        var after = Accept(new DateOnly(2026, 2, 1), Start.AddMinutes(3));
        await SeedAsync(identity, before, from, to, after);
        using var client = fixture.CreateClient(identity);

        var body = await ListAsync(client, "cutOffFrom=2026-01-01&cutOffTo=2026-01-31");

        // Both bounds are inclusive.
        Assert.Equal([to.Id, from.Id], body.Items.Select(i => i.SubmissionId));
    }

    [Fact]
    public async Task List_CurrentOnly_ReturnsOnlyCurrentVersions()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        var january = new DateOnly(2026, 1, 1);
        var olderAccepted = Accept(january, Start);
        var currentJanuary = Accept(january, Start.AddMinutes(1));
        var newerRejected = Reject(january, Start.AddMinutes(2));
        var currentFebruary = Accept(new DateOnly(2026, 2, 1), Start.AddMinutes(3));
        await SeedAsync(identity, olderAccepted, currentJanuary, newerRejected, currentFebruary);
        using var client = fixture.CreateClient(identity);

        var body = await ListAsync(client, "currentOnly=true");

        Assert.Equal([currentFebruary.Id, currentJanuary.Id], body.Items.Select(i => i.SubmissionId));
        Assert.All(body.Items, i => Assert.True(i.IsCurrent));
    }

    [Fact]
    public async Task List_FiltersCombine()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        var january = new DateOnly(2026, 1, 1);
        var olderJanuary = Accept(january, Start);
        var currentJanuary = Accept(january, Start.AddMinutes(1));
        var rejectedJanuary = Reject(january, Start.AddMinutes(2));
        var currentFebruary = Accept(new DateOnly(2026, 2, 1), Start.AddMinutes(3));
        await SeedAsync(identity, olderJanuary, currentJanuary, rejectedJanuary, currentFebruary);
        using var client = fixture.CreateClient(identity);

        var accepted = await ListAsync(client, "status=Accepted&cutOffTo=2026-01-31");
        var currentAccepted = await ListAsync(client, "status=Accepted&cutOffTo=2026-01-31&currentOnly=true");
        var rejectedCurrent = await ListAsync(client, "status=Rejected&currentOnly=true");

        Assert.Equal([currentJanuary.Id, olderJanuary.Id], accepted.Items.Select(i => i.SubmissionId));
        Assert.Equal([currentJanuary.Id], currentAccepted.Items.Select(i => i.SubmissionId));
        // A rejected submission is never current (D-141).
        Assert.Empty(rejectedCurrent.Items);
    }

    [Fact]
    public async Task List_FilteredPages_FollowTheCursor()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        var seeded = Enumerable.Range(0, 9)
            .Select(i => i % 3 == 0 ? Reject(new DateOnly(2026, 1, 1), Start.AddMinutes(i)) : Accept(new DateOnly(2026, 1, 1), Start.AddMinutes(i)))
            .ToArray();
        await SeedAsync(identity, seeded);
        using var client = fixture.CreateClient(identity);

        var pages = await ReadAllPagesAsync(client, "limit=2&status=Accepted");

        var expected = seeded.Where(s => s.Status == SubmissionStatus.Accepted).OrderByDescending(s => s.ReceivedAt).Select(s => s.Id);
        Assert.Equal(expected, pages.SelectMany(p => p.Items).Select(i => i.SubmissionId));
        Assert.Equal([2, 2, 2], pages.Select(p => p.Items.Count));
    }

    [Fact]
    public async Task List_IsCurrentComputedOverAllSubmissions_NotThePage()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        var january = new DateOnly(2026, 1, 1);
        var older = Accept(january, Start);
        var newer = Accept(january, Start.AddMinutes(1));
        await SeedAsync(identity, older, newer);
        using var client = fixture.CreateClient(identity);

        var firstPage = await ListAsync(client, "limit=1");
        var secondPage = await ListAsync(client, $"limit=1&cursor={firstPage.NextCursor}");
        // The newer one is filtered out by the range, but it still makes the older one not current.
        var olderAlone = await ListAsync(client, "status=Accepted&cutOffFrom=2026-01-01&cutOffTo=2026-01-01&limit=1&cursor=" + firstPage.NextCursor);

        Assert.True(Assert.Single(firstPage.Items).IsCurrent);
        Assert.Equal(older.Id, Assert.Single(secondPage.Items).SubmissionId);
        Assert.False(secondPage.Items[0].IsCurrent);
        Assert.False(Assert.Single(olderAlone.Items).IsCurrent);
    }

    [Theory]
    [InlineData("cursor=garbage", "Portfolio.Submissions.InvalidCursor")]
    [InlineData("limit=0", "Portfolio.Submissions.InvalidLimit")]
    [InlineData("limit=101", "Portfolio.Submissions.InvalidLimit")]
    [InlineData("status=Pending", "Portfolio.Submissions.InvalidStatus")]
    [InlineData("cutOffFrom=2026-02-01&cutOffTo=2026-01-01", "Portfolio.Submissions.InvalidCutOffRange")]
    [InlineData("cutOffFrom=yesterday", "Portfolio.Submissions.InvalidCutOffRange")]
    [InlineData("currentOnly=maybe", "Portfolio.Submissions.InvalidCurrentOnly")]
    public async Task List_InvalidParameter_Returns400WithCode(string queryString, string code)
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));

        using var response = await client.GetAsync(new Uri($"{SubmissionTestData.Path}?{queryString}", UriKind.Relative), _ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task List_InvalidCursor_Returns400()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var tampered = new SubmissionCursor(Start, Guid.NewGuid()).Encode()[..^1] + "!";

        using var response = await client.GetAsync(new Uri($"{SubmissionTestData.Path}?cursor={tampered}", UriKind.Relative), _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_OtherTenantsCursor_ReturnsNoForeignRows()
    {
        var (tenantA, seededA) = await SeedTenantAsync(10);
        var cursorFromA = (await ListAsync(tenantA, "limit=3")).NextCursor;
        var identityB = await fixture.ProvisionTenantAsync(_ct);
        var olderB = Accept(new DateOnly(2026, 1, 1), Start.AddMinutes(-5));
        await SeedAsync(identityB, olderB);
        using var tenantB = fixture.CreateClient(identityB);

        var seenByB = await ListAsync(tenantB, $"limit=100&cursor={cursorFromA}");

        // The cursor only moves B's position; the tenant filter keeps A's rows out.
        Assert.Equal([olderB.Id], seenByB.Items.Select(i => i.SubmissionId));
        Assert.DoesNotContain(seenByB.Items, i => seededA.Any(a => a.Id == i.SubmissionId));
    }

    [Fact]
    public async Task ListSubmissions_Always_ReturnsOnlyPublicFields()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        await client.UploadOkAsync("fields.csv", _ct);

        using var response = await client.GetAsync(new Uri(SubmissionTestData.Path, UriKind.Relative), _ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(_ct));

        Assert.Equal(["items", "nextCursor"], json.RootElement.EnumerateObject().Select(p => p.Name));
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal(
            ["cutOffDate", "errorCount", "fileName", "isCurrent", "receivedAt", "rowCount", "sizeBytes", "status", "submissionId"],
            item.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        // A calendar date on the wire, never a timestamp (D-103).
        Assert.Equal(SubmissionTestData.CutOffDate, item.GetProperty("cutOffDate").GetString());
    }

    [Fact]
    public async Task ListSubmissions_IncludesRowAndErrorCounts()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var accepted = await client.UploadOkAsync("accepted.csv", _ct);
        var rejected = await client.UploadOkAsync("rejected.csv", _ct, SubmissionTestData.InvalidCsv());

        var items = (await ListAsync(client)).Items.ToDictionary(i => i.SubmissionId);

        Assert.Equal(("Accepted", 1, 0), (items[accepted.SubmissionId].Status, items[accepted.SubmissionId].RowCount, items[accepted.SubmissionId].ErrorCount));
        Assert.Equal(("Rejected", 2, 3), (items[rejected.SubmissionId].Status, items[rejected.SubmissionId].RowCount, items[rejected.SubmissionId].ErrorCount));
    }

    [Fact]
    public async Task ListSubmissions_NoToken_Returns401()
    {
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync(new Uri(SubmissionTestData.Path, UriKind.Relative), _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Submission Accept(DateOnly cutOffDate, DateTimeOffset receivedAt) =>
        Submission.Accept(Guid.NewGuid(), cutOffDate, "seed.csv", 10, new string('a', Submission.Sha256Length), receivedAt, rowCount: 1);

    private static Submission Reject(DateOnly cutOffDate, DateTimeOffset receivedAt) =>
        Submission.Reject(Guid.NewGuid(), cutOffDate, "seed.csv", 10, new string('b', Submission.Sha256Length), receivedAt,
            rowCount: 1, errorCount: 1);

    // Newest first, same order as the API: received_at DESC, then id DESC (uuid order matches Guid byte order in PostgreSQL).
    private static IEnumerable<Guid> Newest(IEnumerable<Submission> seeded) =>
        seeded.OrderByDescending(s => s.ReceivedAt).ThenByDescending(s => s.Id.ToString(), StringComparer.Ordinal).Select(s => s.Id);

    private async Task<(HttpClient Client, List<Submission> Seeded)> SeedTenantAsync(
        int count, Func<int, DateTimeOffset>? receivedAt = null)
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        var seeded = Enumerable.Range(0, count)
            .Select(i => Accept(new DateOnly(2026, 1, 1), (receivedAt ?? (n => Start.AddMinutes(n)))(i)))
            .ToList();
        await SeedAsync(identity, [.. seeded]);
        return (fixture.CreateClient(identity), seeded);
    }

    // Seeded through the tenant's own context: the stamping interceptor and query filter stay in force.
    private async Task SeedAsync(DevIdentity identity, params Submission[] submissions)
    {
        await using var context = fixture.CreatePortfolioContext(identity.TenantId);
        context.AddRange(submissions);
        await context.SaveChangesAsync(_ct);
    }

    private async Task<List<SubmissionListResponse>> ReadAllPagesAsync(HttpClient client, string queryString)
    {
        var pages = new List<SubmissionListResponse>();
        string? cursor = null;
        do
        {
            var page = await ListAsync(client, cursor is null ? queryString : $"{queryString}&cursor={cursor}");
            pages.Add(page);
            cursor = page.NextCursor;
        }
        while (cursor is not null && pages.Count < 20);

        return pages;
    }

    private async Task<SubmissionListResponse> ListAsync(HttpClient client, string? queryString = null)
    {
        var path = queryString is null ? SubmissionTestData.Path : $"{SubmissionTestData.Path}?{queryString}";
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), _ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SubmissionListResponse>(_ct))!;
    }
}
