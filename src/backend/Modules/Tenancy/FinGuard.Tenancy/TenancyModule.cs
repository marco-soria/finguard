using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Tenancy.Authorization;
using FinGuard.Tenancy.Persistence;
using FinGuard.Tenancy.Resolution;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinGuard.Tenancy;

public static class TenancyModule
{
    public static IHostApplicationBuilder AddTenancyModule(this IHostApplicationBuilder builder)
    {
        // One TenantContext per scope; only Tenancy sets it, everything else reads ITenantContext.
        builder.Services.AddScoped<TenantContext>();
        builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        builder.Services.AddScoped<TenantStampingInterceptor>();

        builder.Services.AddScoped<IAuthorizationHandler, TenantMemberHandler>();
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, TenantAuthorizationResultHandler>();
        builder.Services.AddAuthorizationBuilder().AddPolicy(AuthorizationPolicies.TenantMember, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new TenantMemberRequirement()));

        var connectionString = builder.Configuration.GetRequiredFinGuardConnectionString();
        builder.Services.AddDbContext<TenancyDbContext>(o => o.UseModuleNpgsql(connectionString, TenancyDbContext.Schema));
        builder.EnrichNpgsqlDbContext<TenancyDbContext>(s => s.DisableHealthChecks = true);
        // Lets the Api apply every module's migrations without seeing the internal DbContext types.
        builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TenancyDbContext>());
        return builder;
    }

    /// <summary>Resolves the tenant of the authenticated caller. Call after authentication, before authorization.</summary>
    public static IApplicationBuilder UseTenancy(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();

    public static IEndpointRouteBuilder MapTenancyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/tenancy");
        return endpoints;
    }
}
