var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume();
var database = postgres.AddDatabase("finguard");

var api = builder.AddProject<Projects.FinGuard_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

builder.AddJavaScriptApp("web", "../../frontend/finguard-web", runScriptName: "start")
    .WithPnpm(installArgs: ["--frozen-lockfile"])
    .WithHttpEndpoint(env: "PORT")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
