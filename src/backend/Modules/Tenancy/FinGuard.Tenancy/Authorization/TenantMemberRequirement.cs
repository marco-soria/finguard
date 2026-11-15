using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace FinGuard.Tenancy.Authorization;

/// <summary>The caller is mapped to a tenant.</summary>
internal sealed class TenantMemberRequirement : IAuthorizationRequirement;

internal sealed class TenantMemberHandler(ITenantContext tenantContext) : AuthorizationHandler<TenantMemberRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantMemberRequirement requirement)
    {
        if (tenantContext.HasTenant)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
