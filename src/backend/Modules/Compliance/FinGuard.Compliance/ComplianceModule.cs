using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Compliance.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinGuard.Compliance;

public static class ComplianceModule
{
    public static IHostApplicationBuilder AddComplianceModule(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetRequiredFinGuardConnectionString();
        builder.Services.AddDbContext<ComplianceDbContext>((sp, o) => o
            .UseModuleNpgsql(connectionString, ComplianceDbContext.Schema)
            .AddInterceptors(sp.GetRequiredService<TenantStampingInterceptor>()));
        builder.EnrichNpgsqlDbContext<ComplianceDbContext>(s => s.DisableHealthChecks = true);
        // Lets the Api apply every module's migrations without seeing the internal DbContext types.
        builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ComplianceDbContext>());
        return builder;
    }

    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/compliance");
        return endpoints;
    }
}
