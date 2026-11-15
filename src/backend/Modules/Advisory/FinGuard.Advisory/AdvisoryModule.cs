using FinGuard.Advisory.Persistence;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinGuard.Advisory;

public static class AdvisoryModule
{
    public static IHostApplicationBuilder AddAdvisoryModule(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetRequiredFinGuardConnectionString();
        builder.Services.AddDbContext<AdvisoryDbContext>((sp, o) => o
            .UseModuleNpgsql(connectionString, AdvisoryDbContext.Schema)
            .AddInterceptors(sp.GetRequiredService<TenantStampingInterceptor>()));
        builder.EnrichNpgsqlDbContext<AdvisoryDbContext>(s => s.DisableHealthChecks = true);
        // Lets the Api apply every module's migrations without seeing the internal DbContext types.
        builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AdvisoryDbContext>());
        return builder;
    }

    public static IEndpointRouteBuilder MapAdvisoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/advisory").RequireAuthorization(AuthorizationPolicies.TenantMember);
        return endpoints;
    }
}
