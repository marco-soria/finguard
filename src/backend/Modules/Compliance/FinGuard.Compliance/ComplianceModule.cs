using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Compliance.Domain;
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
        // Singleton options: Wolverine generates handler code for them without service location (ADR-0009).
        builder.Services.AddDbContext<ComplianceDbContext>(
            o => o.UseModuleNpgsql(connectionString, ComplianceDbContext.Schema).AddInterceptors(TenantStampingInterceptor.Instance),
            optionsLifetime: ServiceLifetime.Singleton);
        // EF retries off: Wolverine's Eager transactions refuse EF retry strategies; a Wolverine error policy retries (ADR-0009).
        builder.EnrichNpgsqlDbContext<ComplianceDbContext>(s =>
        {
            s.DisableHealthChecks = true;
            s.DisableRetry = true;
        });
        // Lets the Api apply every module's migrations without seeing the internal DbContext types.
        builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ComplianceDbContext>());
        builder.Services.AddScoped<ILimitPolicyRepository, LimitPolicyRepository>();
        builder.Services.AddScoped<IEvaluationRepository, EvaluationRepository>();
        return builder;
    }

    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/compliance").RequireAuthorization(AuthorizationPolicies.TenantMember);
        return endpoints;
    }
}
