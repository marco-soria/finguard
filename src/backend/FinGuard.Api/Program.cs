using System.Diagnostics;
using FinGuard.Advisory;
using FinGuard.Api.Errors;
using FinGuard.Api.Persistence;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.Compliance;
using FinGuard.Portfolio;
using FinGuard.Tenancy;
using FinGuard.Tenancy.Development;
using Wolverine;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton(TimeProvider.System);
builder.AddNpgsqlDataSource(ModuleDbContextOptions.ConnectionName);

builder.AddTenancyModule();
builder.AddPortfolioModule();
builder.AddComplianceModule();
builder.AddAdvisoryModule();

builder.Host.UseWolverine(opts =>
{
    // Durable message storage only (D-29). Tables are created on startup by the default
    // AutoBuildMessageStorageOnStartup = CreateOrDefault. EF transactional integration is the Phase 1 spike.
    opts.PersistMessagesWithPostgresqlUsingDataSourceFromContainer(schemaName: "wolverine");
    opts.UseRuntimeCompilation();
    opts.Discovery.IncludeAssembly(typeof(TenancyModule).Assembly);
    opts.Discovery.IncludeAssembly(typeof(PortfolioModule).Assembly);
    opts.Discovery.IncludeAssembly(typeof(ComplianceModule).Assembly);
    opts.Discovery.IncludeAssembly(typeof(AdvisoryModule).Assembly);

    if (builder.Environment.IsDevelopment())
    {
        opts.Durability.Mode = DurabilityMode.Solo;
    }
});

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    await app.ApplyModuleMigrationsAsync();
    await app.Services.SeedDevelopmentTenantsAsync(app.Lifetime.ApplicationStopping);
}

app.UseTenancy();

app.MapDefaultEndpoints();
app.MapTenancyEndpoints();
app.MapPortfolioEndpoints();
app.MapComplianceEndpoints();
app.MapAdvisoryEndpoints();

await app.RunAsync();

public partial class Program;
