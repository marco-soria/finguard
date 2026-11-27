using System.Globalization;
using FinGuard.BuildingBlocks.Http;
using FinGuard.BuildingBlocks.Messaging;
using FinGuard.BuildingBlocks.Results;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace FinGuard.Portfolio.Features.ReceiveSubmission;

internal static class ReceiveSubmissionEndpoint
{
    // 5 MB file plus room for the multipart boundaries, headers and the cut-off date field (D-88).
    private const long MaxRequestBytes = 6 * 1024 * 1024;

    public static RouteGroupBuilder MapReceiveSubmission(this RouteGroupBuilder group)
    {
        group.MapPost("/submissions", async (
                [FromForm] IFormFile? file,
                [FromForm] string? cutOffDate,
                IMessageBus bus,
                ITenantContext tenant,
                HttpResponse response,
                CancellationToken ct) =>
            {
                // Binding, not business rules: a missing part or an unparsable date cannot become a command.
                if (file is null)
                {
                    return Result.Failure(ReceiveSubmissionErrors.FileMissing).ToHttpResult();
                }

                if (!DateOnly.TryParseExact(cutOffDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return Result.Failure(ReceiveSubmissionErrors.CutOffDateInvalid).ToHttpResult();
                }

                // Bounded by the request limit below; never written to disk by this code.
                var content = new byte[file.Length];
                await using (var stream = file.OpenReadStream())
                {
                    await stream.ReadExactlyAsync(content, ct);
                }

                var command = new ReceiveSubmission(date, file.FileName, content);
                var result = await bus.InvokeForCurrentTenantAsync<Result<SubmissionReceivedResponse>>(tenant, command, ct);
                return result.ToHttpResult(r => ToHttpResult(r, response));
            })
            // Antiforgery protects cookie-authenticated forms; this API authenticates with JWT bearer tokens only (D-92).
            .DisableAntiforgery()
            // Endpoint metadata (IRequestSizeLimitMetadata) lowers the server body limit for this route only.
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
            .WithFormOptions(multipartBodyLengthLimit: MaxRequestBytes)
            .WithName("ReceiveSubmission")
            .WithSummary("Upload a portfolio")
            .WithDescription("Multipart form with `file` (CSV, UTF-8, at most 5 MB) and `cutOffDate` (yyyy-MM-dd). " +
                "201 stores an Accepted or Rejected submission; 200 with duplicate true returns the earliest submission " +
                "with the same cut-off date and file bytes and stores nothing.")
            .Produces<SubmissionReceivedResponse>(StatusCodes.Status201Created)
            .Produces<SubmissionReceivedResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);
        return group;
    }

    // A duplicate is not a new resource: 200 with the existing submission's Location (D-139).
    private static IResult ToHttpResult(SubmissionReceivedResponse r, HttpResponse response)
    {
        var location = $"{ApiRoutes.V1}/portfolio/submissions/{r.SubmissionId}";
        if (!r.Duplicate)
        {
            return TypedResults.Created(location, r);
        }

        response.Headers.Location = location;
        return TypedResults.Ok(r);
    }
}
