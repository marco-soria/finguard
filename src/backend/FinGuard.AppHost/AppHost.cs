using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume();
var database = postgres.AddDatabase("finguard");

var api = builder.AddDotnetProject("api", "../FinGuard.Api/FinGuard.Api.csproj")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

// Local model for the advisory narrative (ADR-0015). Off with FinGuard:Ai:Enabled=false (`dev-session.ps1 -NoAi`).
// The model name is read like an Aspire parameter (`Parameters:narrator-model`), so it changes without code.
if (builder.Configuration.GetValue("FinGuard:Ai:Enabled", defaultValue: true))
{
    var modelName = builder.Configuration["Parameters:narrator-model"] ?? "llama3.2:3b";
    // The data volume keeps the downloaded model across sessions; never remove it to "clean up".
    var narrator = builder.AddOllama("ollama").WithDataVolume().AddModel("narrator", modelName);
    // No WaitFor: the Api starts and serves template advisories while the model downloads or is stopped.
    api.WithReference(narrator);
}

builder.AddJavaScriptApp("web", "../../frontend/finguard-web", runScriptName: "start")
    .WithPnpm(installArgs: ["--frozen-lockfile"])
    .WithHttpEndpoint(env: "PORT")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
