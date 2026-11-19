using FinGuard.BuildingBlocks.Http;
using FinGuard.BuildingBlocks.Messaging;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace FinGuard.Portfolio.Features.GetSubmission;

internal static class GetSubmissionEndpoint
{
    public static RouteGroupBuilder MapGetSubmission(this RouteGroupBuilder group)
    {
        group.MapGet("/submissions/{id:guid}", async (Guid id, IMessageBus bus, ITenantContext tenant, CancellationToken ct) =>
        {
            var result = await bus.InvokeForCurrentTenantAsync<Result<SubmissionDetailsResponse>>(tenant, new GetSubmission(id), ct);
            return result.ToHttpResult(TypedResults.Ok);
        });
        return group;
    }
}
