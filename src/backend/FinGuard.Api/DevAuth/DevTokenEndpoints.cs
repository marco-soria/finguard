using FinGuard.BuildingBlocks.Http;
using FinGuard.BuildingBlocks.Results;
using FinGuard.Tenancy.Development;

namespace FinGuard.Api.DevAuth;

internal static class DevTokenEndpoints
{
    public const string Path = "/dev/token";

    /// <summary>Maps <c>POST /dev/token</c>. The caller must map it only in Development (R-17).</summary>
    public static IEndpointRouteBuilder MapDevTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Path, (DevTokenRequest request, DevTokenIssuer issuer, TimeProvider timeProvider) =>
                Issue(request, issuer, timeProvider).ToHttpResult(TypedResults.Ok))
            .AllowAnonymous()
            .WithName("IssueDevToken")
            .WithTags("Development")
            .WithSummary("Development token")
            .WithDescription("Development only. Issues a token for a seeded user (alice or bob).")
            .Produces<DevToken>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
        return endpoints;
    }

    private static Result<DevToken> Issue(DevTokenRequest request, DevTokenIssuer issuer, TimeProvider timeProvider) =>
        DevIdentities.Find(request.User) is { } identity
            ? issuer.CreateToken(identity, timeProvider.GetUtcNow())
            : Error.Validation("DevAuth.User.Unknown", "The user is not a seeded development identity.");
}

internal sealed record DevTokenRequest(string? User);
