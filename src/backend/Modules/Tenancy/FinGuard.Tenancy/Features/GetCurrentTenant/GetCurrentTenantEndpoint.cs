using FinGuard.BuildingBlocks.Http;
using FinGuard.BuildingBlocks.Messaging;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace FinGuard.Tenancy.Features.GetCurrentTenant;

internal static class GetCurrentTenantEndpoint
{
    public static RouteGroupBuilder MapGetCurrentTenant(this RouteGroupBuilder group)
    {
        group.MapGet("/me", async (IMessageBus bus, ITenantContext tenant, CancellationToken ct) =>
        {
            var result = await bus.InvokeForCurrentTenantAsync<Result<CurrentTenantResponse>>(tenant, new GetCurrentTenant(), ct);
            return result.ToHttpResult(TypedResults.Ok);
        });
        return group;
    }
}
