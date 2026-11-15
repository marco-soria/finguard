using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinGuard.BuildingBlocks.Tenancy;

/// <summary>
/// Stamps <c>tenant_id</c> on added tenant-owned entities and refuses any cross-tenant write. Stateless: the tenant
/// comes from the intercepted <see cref="TenantAwareDbContext"/>, so module DbContext options can be singletons and
/// Wolverine can generate handler code without service location (ADR-0009, R-19).
/// </summary>
public sealed class TenantStampingInterceptor : SaveChangesInterceptor
{
    public static TenantStampingInterceptor Instance { get; } = new();

    private TenantStampingInterceptor()
    {
    }

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

    private static void Stamp(DbContext? context)
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

            var tenantId = context is TenantAwareDbContext tenantAware
                ? tenantAware.RequiredTenantId
                : throw new InvalidOperationException("Tenant-owned entities must be saved through a TenantAwareDbContext.");
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
