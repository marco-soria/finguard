using System.Net;
using System.Net.Http.Json;
using System.Text;
using FinGuard.Portfolio.Contracts;
using FinGuard.Portfolio.Contracts.Events;
using FinGuard.Portfolio.Features.ReceiveSubmission;
using Wolverine;
using Wolverine.Tracking;

namespace FinGuard.Api.IntegrationTests.Portfolio;

/// <summary>
/// Brief #12 (D-152, D-155): accepted uploads cascade <see cref="SubmissionAccepted"/> through the outbox. The Api host
/// is shared with other test classes running in parallel, so every assertion matches its own submission id.
/// </summary>
[Collection(SubmissionEvents.Collection)]
public sealed class SubmissionAcceptedPublishingTests(ApiFixture fixture)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_ValidCsv_CascadesOneSubmissionAcceptedWithClassTotals()
    {
        var identity = await fixture.ProvisionTenantAsync(_ct);
        using var client = fixture.CreateClient(identity);
        var content = Encoding.UTF8.GetBytes(
            "position_id,asset_class,book_value,currency,description\n"
            + $"{SubmissionTestData.UniquePositionId()},cash,0.10,USD,a\n"
            + "P-2,shares,1250000.55,USD,b\n"
            + "P-3,cash,0.20,USD,c\n"
            + "P-4,real_estate,0,USD,d\n");

        var (session, response) = await SubmissionEvents.UploadTrackedAsync(
            fixture, client, content, "2026-01-31", HttpStatusCode.Created, _ct);

        var published = Assert.Single(SubmissionEvents.Published(session, response.SubmissionId));
        Assert.Equal(
            (identity.TenantId, new DateOnly(2026, 1, 31), response.ReceivedAt, response.ReceivedAt),
            (published.TenantId, published.CutOffDate, published.ReceivedAt, published.OccurredAt));
        Assert.NotEqual(Guid.Empty, published.EventId);
        Assert.Equal(
            [
                new AssetClassTotal(AssetClassCode.Cash, 0.30m),
                new AssetClassTotal(AssetClassCode.FixedIncome, 0m),
                new AssetClassTotal(AssetClassCode.Shares, 1250000.55m),
                new AssetClassTotal(AssetClassCode.RealEstate, 0m),
            ],
            published.Totals);
    }

    [Fact]
    public async Task Upload_InvalidCsv_PublishesNothing()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));

        var (session, response) = await SubmissionEvents.UploadTrackedAsync(
            fixture, client, SubmissionTestData.InvalidCsv(), SubmissionTestData.CutOffDate, HttpStatusCode.Created, _ct);

        Assert.Equal("Rejected", response.Status);
        Assert.Empty(SubmissionEvents.Published(session, response.SubmissionId));
    }

    [Fact]
    public async Task Upload_Duplicate_PublishesNothing()
    {
        using var client = fixture.CreateClient(await fixture.ProvisionTenantAsync(_ct));
        var content = SubmissionTestData.Csv();
        var (first, original) = await SubmissionEvents.UploadTrackedAsync(
            fixture, client, content, SubmissionTestData.CutOffDate, HttpStatusCode.Created, _ct);

        var (second, duplicate) = await SubmissionEvents.UploadTrackedAsync(
            fixture, client, content, SubmissionTestData.CutOffDate, HttpStatusCode.OK, _ct);

        // The original published once; the duplicate answers with its id and adds nothing.
        Assert.Equal((original.SubmissionId, true), (duplicate.SubmissionId, duplicate.Duplicate));
        Assert.Single(SubmissionEvents.Published(first, original.SubmissionId)
            .Concat(SubmissionEvents.Published(second, original.SubmissionId)));
    }
}

/// <summary>Tracked uploads on the shared Api host. One collection, so these classes never interleave.</summary>
internal static class SubmissionEvents
{
    public const string Collection = "SubmissionEvents";

    public static async Task<(ITrackedSession Session, SubmissionReceivedResponse Response)> UploadTrackedAsync(
        ApiFixture fixture, HttpClient client, byte[] content, string cutOffDate, HttpStatusCode expected,
        CancellationToken ct)
    {
        SubmissionReceivedResponse? body = null;
        // Other classes share the host, so their failures may show up in this session: assert on own messages only.
        var session = await fixture.Factory.Services.TrackActivity().DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ =>
            {
                using var response = await client.UploadAsync("portfolio.csv", ct, content, cutOffDate);
                Assert.Equal(expected, response.StatusCode);
                body = await response.Content.ReadFromJsonAsync<SubmissionReceivedResponse>(ct);
            }));
        return (session, body!);
    }

    /// <summary>The events this session sent for one submission.</summary>
    public static IEnumerable<SubmissionAccepted> Published(ITrackedSession session, Guid submissionId) =>
        session.Sent.MessagesOf<SubmissionAccepted>().Where(e => e.SubmissionId == submissionId);

    /// <summary>The events for one submission that a consumer handled successfully in this session.</summary>
    public static IEnumerable<SubmissionAccepted> Consumed(ITrackedSession session, Guid submissionId) =>
        session.MessageSucceeded.MessagesOf<SubmissionAccepted>().Where(e => e.SubmissionId == submissionId);
}
