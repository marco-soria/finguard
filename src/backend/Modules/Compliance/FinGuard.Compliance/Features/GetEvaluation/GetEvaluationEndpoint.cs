using FinGuard.BuildingBlocks.Http;
using FinGuard.BuildingBlocks.Messaging;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace FinGuard.Compliance.Features.GetEvaluation;

internal static class GetEvaluationEndpoint
{
    public static RouteGroupBuilder MapGetEvaluation(this RouteGroupBuilder group)
    {
        group.MapGet("/submissions/{submissionId:guid}/evaluation",
            async (Guid submissionId, IMessageBus bus, ITenantContext tenant, CancellationToken ct) =>
            {
                var result = await bus.InvokeForCurrentTenantAsync<Result<EvaluationResponse>>(
                    tenant, new GetEvaluation(submissionId), ct);
                return result.ToHttpResult(TypedResults.Ok);
            });
        return group;
    }
}
