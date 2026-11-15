namespace FinGuard.BuildingBlocks.Tenancy;

/// <summary>Scoped tenant holder. The tenant can be set once per scope.</summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public bool HasTenant => TenantId.HasValue;

    public Guid RequiredTenantId => TenantId
        ?? throw new InvalidOperationException("No tenant is set for the current scope.");

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("The tenant id must not be empty.", nameof(tenantId));
        }

        if (TenantId is { } current && current != tenantId)
        {
            throw new InvalidOperationException("The tenant is already set to a different tenant for the current scope.");
        }

        TenantId = tenantId;
    }
}
