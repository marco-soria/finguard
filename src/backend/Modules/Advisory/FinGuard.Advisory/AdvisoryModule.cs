using FinGuard.Advisory.Features.WriteAdvisory;
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
        // Singleton options: Wolverine generates handler code for them without service location (ADR-0009).
        builder.Services.AddDbContext<AdvisoryDbContext>(
            o => o.UseModuleNpgsql(connectionString, AdvisoryDbContext.Schema).AddInterceptors(TenantStampingInterceptor.Instance),
            optionsLifetime: ServiceLifetime.Singleton);
        // EF retries off: Wolverine's Eager transactions refuse EF retry strategies; a Wolverine error policy retries (ADR-0009).
        builder.EnrichNpgsqlDbContext<AdvisoryDbContext>(s =>
        {
            s.DisableHealthChecks = true;
            s.DisableRetry = true;
        });
        // Lets the Api apply every module's migrations without seeing the internal DbContext types.
        builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<AdvisoryDbContext>());
        // The only narrator until Brief #19 adds the model (ADR-0006, ADR-0014).
        builder.Services.AddSingleton<TemplateNarrator>();
        return builder;
    }

    public static IEndpointRouteBuilder MapAdvisoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/advisory").RequireAuthorization(AuthorizationPolicies.TenantMember);
        return endpoints;
    }
}
