using FinGuard.BuildingBlocks.Http;
using FinGuard.BuildingBlocks.Messaging;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace FinGuard.Portfolio.Features.ListSubmissions;

internal static class ListSubmissionsEndpoint
{
    public static RouteGroupBuilder MapListSubmissions(this RouteGroupBuilder group)
    {
        group.MapGet("/submissions", async (IMessageBus bus, ITenantContext tenant, CancellationToken ct) =>
        {
            var result = await bus.InvokeForCurrentTenantAsync<Result<SubmissionListResponse>>(tenant, new ListSubmissions(), ct);
            return result.ToHttpResult(TypedResults.Ok);
        });
        return group;
    }
}
