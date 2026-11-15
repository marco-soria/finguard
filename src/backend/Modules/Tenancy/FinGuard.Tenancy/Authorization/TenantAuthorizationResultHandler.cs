using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace FinGuard.Tenancy.Authorization;

/// <summary>
/// Answers 403 with code <c>Tenancy.User.NotProvisioned</c> when an authenticated caller has no tenant. Every other
/// outcome keeps the default behaviour.
/// </summary>
internal sealed class TenantAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    public const string NotProvisionedCode = "Tenancy.User.NotProvisioned";

    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && context.User.Identity?.IsAuthenticated == true
            && authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<TenantMemberRequirement>().Any() == true)
        {
            await TypedResults.Problem(
                    title: "Forbidden",
                    detail: "The signed-in user is not provisioned to a tenant.",
                    statusCode: StatusCodes.Status403Forbidden,
                    extensions: new Dictionary<string, object?> { ["code"] = NotProvisionedCode })
                .ExecuteAsync(context);
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
