using System.Net.Http.Headers;
using FinGuard.Api.DevAuth;
using FinGuard.Api.IntegrationTests.Messaging;
using FinGuard.Api.IntegrationTests.Tenancy;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Tenancy.Development;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public string ConnectionString => _postgres.GetConnectionString();

    /// <summary>The Api with the messaging probe modules, on its own database in the same container.</summary>
    public MessagingHost Messaging { get; private set; } = null!;

    public FakeLogCollector Logs => Factory.Services.GetFakeLogCollector();

    /// <summary>A client carrying a token from the Api's development issuer, issued now unless told otherwise.</summary>
    public HttpClient CreateClient(DevIdentity identity, DateTimeOffset? issuedAt = null)
    {
        var token = Factory.Services.GetRequiredService<DevTokenIssuer>()
            .CreateToken(identity, issuedAt ?? TimeProvider.System.GetUtcNow());
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new ApiFactory(ConnectionString);
        // Boots the host: module migrations and Wolverine storage are created here.
        _ = Factory.Server;

        await using var probe = CreateProbeContext(tenantId: null);
        await probe.Database.ExecuteSqlRawAsync(probe.Database.GenerateCreateScript());

        Messaging = await MessagingHost.StartAsync(ConnectionString);
    }

    /// <summary>
    /// A <see cref="ProbeDbContext"/> with the real <see cref="TenantStampingInterceptor"/>. The tenant is set through a
    /// fresh <see cref="TenantContext"/>, the way the Tenancy middleware sets it for a request.
    /// </summary>
    internal ProbeDbContext CreateProbeContext(Guid? tenantId)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        var options = new DbContextOptionsBuilder<ProbeDbContext>();
        options.UseModuleNpgsql(ConnectionString, ProbeDbContext.Schema)
            .AddInterceptors(TenantStampingInterceptor.Instance);
        return new ProbeDbContext(options.Options, tenantContext);
    }

    public async ValueTask DisposeAsync()
    {
        if (Messaging is not null)
        {
            await Messaging.DisposeAsync();
        }

        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    /// <summary>The Api as tests boot it. Internal so a test can start a second instance on real Kestrel.</summary>
    internal sealed class ApiFactory(string connectionString, Action<IWebHostBuilder>? configure = null)
        : WebApplicationFactory<Program>
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
                services.AddTransient<IStartupFilter, TenantProbeStartupFilter>();
            });
            configure?.Invoke(builder);
        }
    }
}
