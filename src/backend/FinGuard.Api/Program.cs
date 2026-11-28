using System.Diagnostics;
using FinGuard.Advisory;
using FinGuard.Api.ApiDocs;
using FinGuard.Api.DevAuth;
using FinGuard.Api.Errors;
using FinGuard.Api.Messaging;
using FinGuard.Api.Persistence;
using FinGuard.Api.RateLimiting;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.Compliance;
using FinGuard.Portfolio;
using FinGuard.Tenancy;
using FinGuard.Tenancy.Development;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton(TimeProvider.System);
builder.AddNpgsqlDataSource(ModuleDbContextOptions.ConnectionName);

builder.AddTenancyModule();
builder.AddPortfolioModule();
builder.AddComplianceModule();
builder.AddAdvisoryModule();
builder.AddFinGuardAuthentication();

builder.AddFinGuardMessaging();
builder.AddFinGuardApiDocs();
builder.AddFinGuardRateLimiting();
builder.AddFinGuardForwardedHeaders();

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseFinGuardForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    await app.ApplyModuleMigrationsAsync();
    await app.Services.SeedDevelopmentTenantsAsync(app.Lifetime.ApplicationStopping);
}

app.UseAuthentication();
app.UseTenancy();
// After tenancy so the tenant is known; before authorization so anonymous floods are counted before the 401 (D-313).
app.UseRateLimiter();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapFinGuardApiDocs();
if (app.Environment.IsDevelopment())
{
    app.MapDevTokenEndpoints();
}

app.MapTenancyEndpoints();
app.MapPortfolioEndpoints();
app.MapComplianceEndpoints();
app.MapAdvisoryEndpoints();

await app.RunAsync();

public partial class Program;
