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
    private static readonly string Description =
        "One page of the tenant's submissions, newest first (received time, then id). Query parameters, all optional " +
        "and combined with AND: `limit` (1 to 100, default 25), `cursor` (the previous page's `nextCursor`), `status` " +
        "(`Accepted` or `Rejected`), `cutOffFrom` and `cutOffTo` (inclusive, yyyy-MM-dd), `currentOnly` (true or false). " +
        "`isCurrent` is computed over all of the tenant's submissions, not the page. `nextCursor` is null on the last " +
        "page.\n\n400 codes, checked in this order (the first failing rule answers): " +
        string.Join(", ", ListSubmissionsErrors.All.Select(e => $"`{e.Code}`")) + ".";

    public static RouteGroupBuilder MapListSubmissions(this RouteGroupBuilder group)
    {
        // Query values bind as strings; the handler validates them (one Portfolio.Submissions.* code per rule).
        group.MapGet("/submissions", async (
                string? limit, string? cursor, string? status, string? cutOffFrom, string? cutOffTo, string? currentOnly,
                IMessageBus bus, ITenantContext tenant, CancellationToken ct) =>
            {
                var query = new ListSubmissions(limit, cursor, status, cutOffFrom, cutOffTo, currentOnly);
                var result = await bus.InvokeForCurrentTenantAsync<Result<SubmissionListResponse>>(tenant, query, ct);
                return result.ToHttpResult(TypedResults.Ok);
            })
            .WithName("ListSubmissions")
            .WithSummary("Submission history")
            .WithDescription(Description)
            .Produces<SubmissionListResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        return group;
    }
}
