using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume();
var database = postgres.AddDatabase("finguard");

var api = builder.AddDotnetProject("api", "../FinGuard.Api/FinGuard.Api.csproj")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

// Local model for the AI context paragraph (ADR-0015). Off by default (D-293): opt in with FinGuard:Ai:Enabled=true
// (`dev-session.ps1 -StartAspire -WithAi`).
// The model name is read like an Aspire parameter (`Parameters:narrator-model`), so it changes without code.
if (builder.Configuration.GetValue("FinGuard:Ai:Enabled", defaultValue: false))
{
    var modelName = builder.Configuration["Parameters:narrator-model"] ?? "llama3.2:3b";
    // The data volume keeps the downloaded model across sessions; never remove it to "clean up".
    var narrator = builder.AddOllama("ollama")
        .WithDataVolume()
        // Unload the model (about 3 GB of RAM) after two idle minutes instead of Ollama's default five.
        .WithEnvironment("OLLAMA_KEEP_ALIVE", "2m")
        .AddModel("narrator", modelName);
    // No WaitFor: the Api starts and serves template advisories while the model downloads or is stopped.
    api.WithReference(narrator);
}

builder.AddJavaScriptApp("web", "../../frontend/finguard-web", runScriptName: "start")
    .WithPnpm(installArgs: ["--frozen-lockfile"])
    .WithHttpEndpoint(env: "PORT")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
