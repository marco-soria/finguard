using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(FinGuard.Api.IntegrationTests.ApiFixture))]

namespace FinGuard.Api.IntegrationTests;

/// <summary>One PostgreSQL container and one booted Api per test assembly (D-52).</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    // Same tag Aspire uses for the local postgres resource.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.3").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public string ConnectionString => _postgres.GetConnectionString();

    public FakeLogCollector Logs => Factory.Services.GetFakeLogCollector();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new ApiFactory(ConnectionString);
        // Boots the host: module migrations and Wolverine storage are created here.
        _ = Factory.Server;
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    private sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:finguard", connectionString);
            // Blank cloud settings so local user-secrets or environment variables cannot switch a seam on.
            builder.UseSetting("KeyVault:Uri", string.Empty);
            builder.UseSetting("APPLICATIONINSIGHTS_CONNECTION_STRING", string.Empty);
            builder.ConfigureServices(services =>
            {
                services.AddLogging(logging => logging.AddFakeLogging());
                services.AddTransient<IStartupFilter, ThrowingEndpointStartupFilter>();
            });
        }
    }
}
