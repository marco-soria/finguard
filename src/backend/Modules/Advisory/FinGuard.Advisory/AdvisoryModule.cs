using FinGuard.Advisory.Features.GetAdvisory;
using FinGuard.Advisory.Features.Refinement;
using FinGuard.Advisory.Features.WriteAdvisory;
using FinGuard.Advisory.Infrastructure;
using FinGuard.Advisory.Persistence;
using FinGuard.BuildingBlocks.Http;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wolverine;

namespace FinGuard.Advisory;

public static class AdvisoryModule
{
    /// <summary>Model calls run one at a time on their own queue, so they never hold up other messages (ADR-0015).</summary>
    public const string RefinementQueue = "advisory-refinement";

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
        // The template always writes the narrative (ADR-0014); the model adds a context paragraph only when configured (D-293).
        builder.Services.AddSingleton<TemplateNarrator>();
        var modelEnabled = builder.AddNarratorModel();
        // Without a model no RefineNarrative is ever sent; the template only satisfies the handler's dependency.
        builder.Services.TryAddSingleton<IAdvisoryNarrator>(sp => sp.GetRequiredService<TemplateNarrator>());
        builder.Services.AddOptions<AdvisoryOptions>()
            .BindConfiguration(AdvisoryOptions.Section)
            .Configure(o => o.ModelEnabled = modelEnabled)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return builder;
    }

    /// <summary>Routes the model call to <see cref="RefinementQueue"/>, one message at a time (durable like every local queue).</summary>
    public static WolverineOptions AddAdvisoryMessaging(this WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PublishMessage<RefineNarrative>().ToLocalQueue(RefinementQueue).MaximumParallelMessages(1);
        return options;
    }

    public static IEndpointRouteBuilder MapAdvisoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup($"{ApiRoutes.V1}/advisory")
            .RequireAuthorization(AuthorizationPolicies.TenantMember)
            .MapGetAdvisory();
        return endpoints;
    }
}
