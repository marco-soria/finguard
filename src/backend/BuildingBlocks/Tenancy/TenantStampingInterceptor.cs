using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinGuard.BuildingBlocks.Tenancy;

/// <summary>Stamps <c>tenant_id</c> on added tenant-owned entities and refuses any cross-tenant write.</summary>
public sealed class TenantStampingInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var tenantId = tenantContext.RequiredTenantId;
            var property = entry.Property(e => e.TenantId);

            if (entry.State == EntityState.Added)
            {
                if (property.CurrentValue == Guid.Empty)
                {
                    property.CurrentValue = tenantId;
                }
                else if (property.CurrentValue != tenantId)
                {
                    throw new InvalidOperationException("An added entity belongs to a different tenant.");
                }
            }
            else if (property.IsModified && property.CurrentValue != property.OriginalValue)
            {
                throw new InvalidOperationException("The tenant of an existing entity must not change.");
            }
            else if (property.OriginalValue != tenantId)
            {
                throw new InvalidOperationException("A modified entity belongs to a different tenant.");
            }
        }
    }
}
