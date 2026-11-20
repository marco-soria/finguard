using System.Net;
using System.Net.Http.Json;
using FinGuard.Portfolio.Features.GetSubmission;
using FinGuard.Portfolio.Features.ListSubmissions;

namespace FinGuard.Api.IntegrationTests.Portfolio;

// Brief #11 (D-141): current = latest accepted per tenant and cut-off date by received_at (tie: id), derived on read.
public sealed class CurrentSubmissionTests(ApiFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListSubmissions_TwoAcceptedSameDate_NewestIsCurrent()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var older = await client.UploadOkAsync("older.csv", _ct);
        var newer = await client.UploadOkAsync("newer.csv", _ct);
        var otherDate = await client.UploadOkAsync("other-date.csv", _ct, cutOffDate: "2026-01-02");

        var current = await CurrentByIdAsync(client);

        Assert.Equal(
            [(older.SubmissionId, false), (newer.SubmissionId, true), (otherDate.SubmissionId, true)],
            new[] { older, newer, otherDate }.Select(s => (s.SubmissionId, current[s.SubmissionId])));
    }

    [Fact]
    public async Task ListSubmissions_NewerRejectedSameDate_DoesNotChangeCurrent()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var accepted = await client.UploadOkAsync("accepted.csv", _ct);
        var rejected = await client.UploadOkAsync("rejected.csv", _ct, SubmissionTestData.InvalidCsv());

        var current = await CurrentByIdAsync(client);

        Assert.True(current[accepted.SubmissionId]);
        Assert.False(current[rejected.SubmissionId]);
    }

    [Fact]
    public async Task ListSubmissions_OnlyRejected_NoneIsCurrent()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var rejected = await client.UploadOkAsync("rejected.csv", _ct, SubmissionTestData.InvalidCsv());

        var current = await CurrentByIdAsync(client);

        Assert.False(current[rejected.SubmissionId]);
    }

    [Fact]
    public async Task ListSubmissions_IsCurrent_IgnoresOtherTenantsSubmissions()
    {
        using var alice = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        using var bob = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var alicesId = (await alice.UploadOkAsync("alice.csv", _ct)).SubmissionId;
        var bobsId = (await bob.UploadOkAsync("bob.csv", _ct)).SubmissionId;

        Assert.True((await CurrentByIdAsync(alice))[alicesId]);
        Assert.True((await CurrentByIdAsync(bob))[bobsId]);
        Assert.True((await GetAsync(alice, alicesId)).IsCurrent);
    }

    [Fact]
    public async Task GetSubmission_IsCurrent_MatchesList()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var ids = new[]
        {
            (await client.UploadOkAsync("older.csv", _ct)).SubmissionId,
            (await client.UploadOkAsync("newer.csv", _ct)).SubmissionId,
            (await client.UploadOkAsync("rejected.csv", _ct, SubmissionTestData.InvalidCsv())).SubmissionId,
        };

        var fromList = await CurrentByIdAsync(client);
        var fromDetails = new List<bool>();
        foreach (var id in ids)
        {
            fromDetails.Add((await GetAsync(client, id)).IsCurrent);
        }

        Assert.Equal([false, true, false], fromDetails);
        Assert.Equal(ids.Select(id => fromList[id]), fromDetails);
    }

    private async Task<Dictionary<Guid, bool>> CurrentByIdAsync(HttpClient client)
    {
        using var response = await client.GetAsync(new Uri(SubmissionTestData.Path, UriKind.Relative), _ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<SubmissionListResponse>(_ct))!;
        return body.Items.ToDictionary(i => i.SubmissionId, i => i.IsCurrent);
    }

    private async Task<SubmissionDetailsResponse> GetAsync(HttpClient client, Guid id)
    {
        using var response = await client.GetAsync(new Uri($"{SubmissionTestData.Path}/{id}", UriKind.Relative), _ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SubmissionDetailsResponse>(_ct))!;
    }
}
