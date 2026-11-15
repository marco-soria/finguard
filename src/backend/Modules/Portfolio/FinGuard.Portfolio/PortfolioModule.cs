using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
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
        builder.Services.AddDbContext<PortfolioDbContext>((sp, o) => o
            .UseModuleNpgsql(connectionString, PortfolioDbContext.Schema)
            .AddInterceptors(sp.GetRequiredService<TenantStampingInterceptor>()));
        builder.EnrichNpgsqlDbContext<PortfolioDbContext>(s => s.DisableHealthChecks = true);
        // Lets the Api apply every module's migrations without seeing the internal DbContext types.
        builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<PortfolioDbContext>());
        return builder;
    }

    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/portfolio").RequireAuthorization(AuthorizationPolicies.TenantMember);
        return endpoints;
    }
}
