namespace FinGuard.BuildingBlocks.Tenancy;

/// <summary>A tenant context that never has a tenant. Used by design-time DbContext factories.</summary>
public sealed class NoTenantContext : ITenantContext
{
    public static NoTenantContext Instance { get; } = new();

    private NoTenantContext()
    {
    }

    public Guid? TenantId => null;

    public bool HasTenant => false;

    public Guid RequiredTenantId => throw new InvalidOperationException("No tenant is set for the current scope.");
}
