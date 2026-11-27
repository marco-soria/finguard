using FinGuard.BuildingBlocks.Http;
using FinGuard.BuildingBlocks.Messaging;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace FinGuard.Advisory.Features.GetAdvisory;

internal static class GetAdvisoryEndpoint
{
    public static RouteGroupBuilder MapGetAdvisory(this RouteGroupBuilder group)
    {
        group.MapGet("/submissions/{submissionId:guid}/advisory",
            async (Guid submissionId, IMessageBus bus, ITenantContext tenant, CancellationToken ct) =>
            {
                var result = await bus.InvokeForCurrentTenantAsync<Result<AdvisoryResponse>>(
                    tenant, new GetAdvisory(submissionId), ct);
                return result.ToHttpResult(TypedResults.Ok);
            })
            .WithName("GetAdvisory")
            .WithSummary("Rebalancing advisory")
            .WithDescription("The plain-text narrative of an evaluation and the optional validated AI context paragraph.")
            .Produces<AdvisoryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        return group;
    }
}
