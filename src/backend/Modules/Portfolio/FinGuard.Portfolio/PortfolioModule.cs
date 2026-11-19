using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Portfolio.Features.ReceiveSubmission;
using FinGuard.Portfolio.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinGuard.Portfolio;

public static class PortfolioModule
{
    public static IHostApplicationBuilder AddPortfolioModule(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetRequiredFinGuardConnectionString();
        // Singleton options: Wolverine generates handler code for them without service location (ADR-0009).
        builder.Services.AddDbContext<PortfolioDbContext>(
            o => o.UseModuleNpgsql(connectionString, PortfolioDbContext.Schema).AddInterceptors(TenantStampingInterceptor.Instance),
            optionsLifetime: ServiceLifetime.Singleton);
        // EF retries off: Wolverine's Eager transactions refuse EF retry strategies; a Wolverine error policy retries (ADR-0009).
        builder.EnrichNpgsqlDbContext<PortfolioDbContext>(s =>
        {
            s.DisableHealthChecks = true;
            s.DisableRetry = true;
        });
        // Lets the Api apply every module's migrations without referencing each module's DbContext type.
        builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<PortfolioDbContext>());
        return builder;
    }

    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/portfolio")
            .RequireAuthorization(AuthorizationPolicies.TenantMember)
            .MapReceiveSubmission();
        return endpoints;
    }
}
