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
                return result.ToHttpResult(r => TypedResults.Created($"/api/portfolio/submissions/{r.SubmissionId}", r));
            })
            // Antiforgery protects cookie-authenticated forms; this API authenticates with JWT bearer tokens only (D-92).
            .DisableAntiforgery()
            // Endpoint metadata (IRequestSizeLimitMetadata) lowers the server body limit for this route only.
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
            .WithFormOptions(multipartBodyLengthLimit: MaxRequestBytes);
        return group;
    }
}
