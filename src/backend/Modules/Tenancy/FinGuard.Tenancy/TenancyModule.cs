using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Tenancy.Authorization;
using FinGuard.Tenancy.Features.GetCurrentTenant;
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
        // One TenantContext per scope; only Tenancy and the BuildingBlocks messaging middleware set it, everything else
        // reads ITenantContext. ITenantContext is the type-based registration so Wolverine's generated code builds one
        // instance per handler and shares it with the middleware and the DbContexts (ADR-0009, R-19).
        builder.Services.AddScoped<ITenantContext, TenantContext>();
        builder.Services.AddScoped(sp => (TenantContext)sp.GetRequiredService<ITenantContext>());

        builder.Services.AddScoped<IAuthorizationHandler, TenantMemberHandler>();
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, TenantAuthorizationResultHandler>();
        builder.Services.AddAuthorizationBuilder().AddPolicy(AuthorizationPolicies.TenantMember, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new TenantMemberRequirement()));

        var connectionString = builder.Configuration.GetRequiredFinGuardConnectionString();
        builder.Services.AddDbContext<TenancyDbContext>(
            o => o.UseModuleNpgsql(connectionString, TenancyDbContext.Schema), optionsLifetime: ServiceLifetime.Singleton);
        // EF retries off: Wolverine's Eager transactions refuse EF retry strategies; a Wolverine error policy retries (ADR-0009).
        builder.EnrichNpgsqlDbContext<TenancyDbContext>(s =>
        {
            s.DisableHealthChecks = true;
            s.DisableRetry = true;
        });
        // Lets the Api apply every module's migrations without seeing the internal DbContext types.
        builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TenancyDbContext>());
        return builder;
    }

    /// <summary>Resolves the tenant of the authenticated caller. Call after authentication, before authorization.</summary>
    public static IApplicationBuilder UseTenancy(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();

    public static IEndpointRouteBuilder MapTenancyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/tenancy")
            .RequireAuthorization(AuthorizationPolicies.TenantMember)
            .MapGetCurrentTenant();
        return endpoints;
    }
}
