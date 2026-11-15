using System.Net;
using System.Net.Http.Json;
using Dapper;
using FinGuard.Tenancy.Development;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wolverine;
using Wolverine.Tracking;

namespace FinGuard.Api.IntegrationTests.Messaging;

// Brief #6 spike tests, kept as regression tests (ADR-0009). Every test uses fresh ids, so rows from other tests in the
// shared probe tables do not interfere. DB assertions use raw SQL so they see rows regardless of query filters.
public sealed class OutboxAndTenantPropagationTests(ApiFixture fixture)
{
    private static readonly Guid Acme = DevIdentities.Alice.TenantId;
    private static readonly Guid Globex = DevIdentities.Bob.TenantId;

    private readonly MessagingHost _host = fixture.Messaging;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task PlaceOrder_ForTenant_WritesOrderAndLedgerEntry()
    {
        var orderId = Guid.NewGuid();

        var session = await _host.Services.TrackActivity()
            .ExecuteAndWaitAsync(_ => InvokeForTenantAsync(Acme, new PlaceProbeOrder(orderId, "R-1", false)));

        Assert.Single(session.Executed.MessagesOf<ProbeOrderPlaced>());
        Assert.Equal(Acme, await OrderTenantAsync(orderId));
        Assert.Equal([Acme], await LedgerTenantsAsync(orderId));
    }

    [Fact]
    public async Task PlaceOrder_HandlerThrowsAfterChanges_WritesNothingAndPublishesNothing()
    {
        var orderId = Guid.NewGuid();
        var envelopesBefore = await ProbeEventEnvelopeCountAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _host.Services.TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync(_ => InvokeForTenantAsync(Acme, new PlaceProbeOrder(orderId, "R-2", true))));

        Assert.Equal("Probe failure after save.", exception.Message);
        Assert.Null(await OrderTenantAsync(orderId));
        Assert.Empty(await LedgerTenantsAsync(orderId));
        Assert.True(await ProbeEventEnvelopeCountAsync() <= envelopesBefore, "No outgoing or incoming envelope may be written.");
    }

    [Fact]
    public async Task OrderPlaced_ConsumerFailsTransientlyTwice_IsRetriedAndWritesOnce()
    {
        var orderId = Guid.NewGuid();
        ProbeOrderPlacedHandler.FailTransiently(2);

        var session = await _host.Services.TrackActivity().DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync(_ => InvokeForTenantAsync(Acme, new PlaceProbeOrder(orderId, "R-3", false)));

        var records = session.AllRecordsInOrder().ToList();
        var commandDone = records.FindIndex(r =>
            r.MessageEventType == MessageEventType.ExecutionFinished && r.Message is PlaceProbeOrder);
        var eventAttempts = records.FindAll(r =>
            r.MessageEventType == MessageEventType.ExecutionStarted && r.Message is ProbeOrderPlaced);
        // Two inline retries after two transient faults. Decoupling: every consumer attempt starts after the command
        // finished and committed, so the order exists whatever the consumer does.
        Assert.True(commandDone >= 0, "The command must finish.");
        Assert.Equal(3, eventAttempts.Count);
        Assert.All(eventAttempts, a => Assert.True(records.IndexOf(a) > commandDone));
        Assert.Single(session.MessageSucceeded.MessagesOf<ProbeOrderPlaced>());
        Assert.Equal(Acme, await OrderTenantAsync(orderId));
        Assert.Equal([Acme], await LedgerTenantsAsync(orderId));
    }

    [Fact]
    public async Task OrderPlaced_DeliveredTwiceWithSameEventId_WritesOneLedgerEntry()
    {
        var orderId = Guid.NewGuid();
        var @event = new ProbeOrderPlaced(Guid.NewGuid(), Acme, DateTimeOffset.UtcNow, orderId, orderId.ToString());

        await InvokeForTenantAsync(Acme, @event);
        await InvokeForTenantAsync(Acme, @event);

        Assert.Equal([Acme], await LedgerTenantsAsync(orderId));
    }

    [Fact]
    public async Task PlaceOrder_ViaHttpWithAliceToken_StampsAcmeOnBothRows()
    {
        var orderId = Guid.NewGuid();
        using var client = _host.CreateClient(DevIdentities.Alice);
        HttpResponseMessage? response = null;

        var session = await _host.Services.TrackActivity().ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ =>
            response = await client.PostAsJsonAsync(ProbeOrderRoute.Path, new PlaceProbeOrderRequest(orderId, "R-5"), _ct)));

        using (response)
        {
            Assert.Equal(HttpStatusCode.NoContent, response!.StatusCode);
        }

        // One TenantContext per handler scope: the middleware set it, the handler read it into the event payload and
        // the stamping interceptor of each DbContext stamped it (R-16, R-19).
        var placed = Assert.Single(session.MessageSucceeded.MessagesOf<ProbeOrderPlaced>());
        Assert.Equal(Acme, placed.TenantId);
        Assert.Equal(Acme, await OrderTenantAsync(orderId));
        Assert.Equal([Acme], await LedgerTenantsAsync(orderId));
    }

    [Fact]
    public async Task PlaceOrder_AliceThenBob_EachLedgerSeesOnlyOwnEntries()
    {
        var aliceOrder = Guid.NewGuid();
        var bobOrder = Guid.NewGuid();
        await PlaceViaHttpAsync(DevIdentities.Alice, aliceOrder);
        await PlaceViaHttpAsync(DevIdentities.Bob, bobOrder);

        var aliceSees = await VisibleLedgerOrdersAsync(Acme, aliceOrder, bobOrder);
        var bobSees = await VisibleLedgerOrdersAsync(Globex, aliceOrder, bobOrder);

        Assert.Equal([aliceOrder.ToString()], aliceSees);
        Assert.Equal([bobOrder.ToString()], bobSees);
    }

    [Fact]
    public async Task OrderPlaced_WithoutTenant_FailsClosedAndWritesNothing()
    {
        var orderId = Guid.NewGuid();
        var @event = new ProbeOrderPlaced(Guid.NewGuid(), Acme, DateTimeOffset.UtcNow, orderId, orderId.ToString());

        // Published to the durable local queue with no envelope tenant: the stamping interceptor must refuse the write.
        var session = await _host.Services.TrackActivity().DoNotAssertOnExceptionsDetected()
            .ExecuteAndWaitAsync(context => context.PublishAsync(@event).AsTask());

        var failure = Assert.Single(session.AllRecordsInOrder(),
            r => r.Message is ProbeOrderPlaced && r.MessageEventType == MessageEventType.MovedToErrorQueue);
        Assert.IsType<InvalidOperationException>(failure.Exception);
        Assert.Empty(session.MessageSucceeded.MessagesOf<ProbeOrderPlaced>());
        Assert.Empty(await LedgerTenantsAsync(orderId));
    }

    [Fact]
    public async Task OrderPlaced_PayloadTenantDiffersFromEnvelope_IsRejected()
    {
        var orderId = Guid.NewGuid();
        var @event = new ProbeOrderPlaced(Guid.NewGuid(), Globex, DateTimeOffset.UtcNow, orderId, orderId.ToString());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeForTenantAsync(Acme, @event));

        Assert.Equal("The message tenant id differs from the envelope tenant id.", exception.Message);
        Assert.Empty(await LedgerTenantsAsync(orderId));
    }

    private async Task InvokeForTenantAsync(Guid tenantId, object message)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeForTenantAsync(tenantId.ToString(), message, _ct);
    }

    private async Task PlaceViaHttpAsync(DevIdentity identity, Guid orderId)
    {
        using var client = _host.CreateClient(identity);
        await _host.Services.TrackActivity().ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ =>
        {
            using var response = await client.PostAsJsonAsync(
                ProbeOrderRoute.Path, new PlaceProbeOrderRequest(orderId, orderId.ToString()), _ct);
            response.EnsureSuccessStatusCode();
        }));
    }

    // Through the ledger context, so the tenant query filter decides what is visible.
    private async Task<List<string>> VisibleLedgerOrdersAsync(Guid tenantId, params Guid[] orderIds)
    {
        var references = orderIds.Select(id => id.ToString()).ToList();
        await using var ledger = _host.CreateLedgerContext(tenantId);
        return await ledger.Entries.Where(e => references.Contains(e.Reference)).Select(e => e.Reference)
            .ToListAsync(_ct);
    }

    private async Task<Guid?> OrderTenantAsync(Guid orderId)
    {
        await using var connection = new NpgsqlConnection(_host.ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT tenant_id FROM probe_orders.probe_orders WHERE id = @orderId", new { orderId }, cancellationToken: _ct));
    }

    // Ledger rows carry the order reference; each test uses a unique reference derived from the order.
    private async Task<List<Guid>> LedgerTenantsAsync(Guid orderId)
    {
        await using var connection = new NpgsqlConnection(_host.ConnectionString);
        var rows = await connection.QueryAsync<Guid>(new CommandDefinition(
            """
            SELECT l.tenant_id FROM probe_ledger.probe_ledger_entries l
            WHERE l.reference = (SELECT o.reference FROM probe_orders.probe_orders o WHERE o.id = @orderId)
               OR l.reference = @reference
            """,
            new { orderId, reference = orderId.ToString() }, cancellationToken: _ct));
        return rows.ToList();
    }

    private async Task<long> ProbeEventEnvelopeCountAsync()
    {
        await using var connection = new NpgsqlConnection(_host.ConnectionString);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT (SELECT count(*) FROM wolverine.wolverine_outgoing_envelopes WHERE message_type LIKE '%ProbeOrderPlaced%')
                 + (SELECT count(*) FROM wolverine.wolverine_incoming_envelopes WHERE message_type LIKE '%ProbeOrderPlaced%')
            """, cancellationToken: _ct));
    }
}
