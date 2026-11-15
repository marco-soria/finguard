using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinGuard.Api.IntegrationTests.Messaging;

public sealed record PlaceProbeOrder(Guid OrderId, string Reference, bool FailAfterSave);

/// <summary>Shaped like an integration event (ADR-0009).</summary>
public sealed record ProbeOrderPlaced(Guid EventId, Guid TenantId, DateTimeOffset OccurredAt, Guid OrderId, string Reference);

/// <summary>Writes through the orders context only; the event cascades through the outbox.</summary>
public static class PlaceProbeOrderHandler
{
    public static async Task<ProbeOrderPlaced> Handle(
        PlaceProbeOrder command, ProbeOrdersDbContext db, ITenantContext tenant, TimeProvider time, CancellationToken ct)
    {
        db.Orders.Add(ProbeOrder.Create(command.OrderId, command.Reference));

        if (command.FailAfterSave)
        {
            // Flush first, so the test proves the Eager transaction rolls back rows already sent to the database.
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Probe failure after save.");
        }

        return new ProbeOrderPlaced(Guid.NewGuid(), tenant.RequiredTenantId, time.GetUtcNow(), command.OrderId, command.Reference);
    }
}

/// <summary>Writes through the ledger context only; idempotent by <see cref="ProbeOrderPlaced.EventId"/>.</summary>
public static class ProbeOrderPlacedHandler
{
    private static int _transientFailuresLeft;

    /// <summary>Test switch: the next <paramref name="count"/> attempts throw a transient database fault.</summary>
    public static void FailTransiently(int count) => Volatile.Write(ref _transientFailuresLeft, count);

    public static async Task Handle(ProbeOrderPlaced @event, ProbeLedgerDbContext db, CancellationToken ct)
    {
        if (Interlocked.Decrement(ref _transientFailuresLeft) >= 0)
        {
            // NpgsqlException.IsTransient is true for a TimeoutException inner exception.
            throw new NpgsqlException("Probe transient failure.", new TimeoutException());
        }

        Interlocked.Exchange(ref _transientFailuresLeft, 0);

        if (await db.Entries.AnyAsync(e => e.SourceEventId == @event.EventId, ct))
        {
            return;
        }

        db.Entries.Add(ProbeLedgerEntry.Create(@event.EventId, @event.Reference));
    }
}
