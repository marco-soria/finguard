namespace FinGuard.BuildingBlocks.Tenancy;

/// <summary>An entity that belongs to one tenant. Filtered and stamped by <see cref="TenantAwareDbContext"/>.</summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
