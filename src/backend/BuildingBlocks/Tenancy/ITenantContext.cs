namespace FinGuard.BuildingBlocks.Tenancy;

/// <summary>The tenant of the current scope. Set only by the Tenancy module (ADR-0004).</summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    bool HasTenant { get; }

    /// <summary>The current tenant. Throws when there is none: reaching tenant-owned data without a tenant is a bug.</summary>
    Guid RequiredTenantId { get; }
}
