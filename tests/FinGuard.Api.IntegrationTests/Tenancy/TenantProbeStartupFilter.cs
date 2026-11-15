using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FinGuard.Api.IntegrationTests.Tenancy;

/// <summary>
/// Test-only route that returns the resolved tenant. Added after the Api pipeline, so the Api's authentication and
/// tenant resolution have already run; its own routing and authorization then apply the TenantMember policy.
/// </summary>
internal sealed class TenantProbeStartupFilter : IStartupFilter
{
    public const string Path = "/api/_test/tenant";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(endpoints => endpoints
            .MapGet(Path, (ITenantContext tenant) => TypedResults.Ok(new TenantProbeResponse(tenant.RequiredTenantId)))
            .RequireAuthorization(AuthorizationPolicies.TenantMember));
    };
}

internal sealed record TenantProbeResponse(Guid TenantId);
