namespace FinGuard.BuildingBlocks.Tenancy;

public static class TenantQueryFilter
{
    /// <summary>Name of the EF Core query filter applied to every <see cref="ITenantOwned"/> entity.</summary>
    public const string Name = "Tenant";
}
