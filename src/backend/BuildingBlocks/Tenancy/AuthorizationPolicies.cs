namespace FinGuard.BuildingBlocks.Tenancy;

public static class AuthorizationPolicies
{
    /// <summary>The caller is authenticated and mapped to a tenant. Required by every module endpoint group.</summary>
    public const string TenantMember = "TenantMember";
}
