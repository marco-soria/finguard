using System.Net.Http.Headers;
using FinGuard.Api.DevAuth;
using FinGuard.BuildingBlocks.Messaging;
using FinGuard.BuildingBlocks.Persistence;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Tenancy.Development;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wolverine;

namespace FinGuard.Api.IntegrationTests.Messaging;

/// <summary>
/// A second Api host with the probe modules, on its own database in the shared container. Two Wolverine hosts in Solo
/// mode on one envelope store would both claim the durability agents and recover each other's envelopes.
/// </summary>
public sealed class MessagingHost : IAsyncDisposable
{
    public const string DatabaseName = "finguard_messaging";

    private readonly WebApplicationFactory<Program> _factory;

    private MessagingHost(string connectionString)
    {
        ConnectionString = connectionString;
        _factory = new MessagingFactory(connectionString);
    }

    public string ConnectionString { get; }

    public IServiceProvider Services => _factory.Services;

    public static async Task<MessagingHost> StartAsync(string serverConnectionString)
    {
        await using (var server = new NpgsqlConnection(serverConnectionString))
        {
            await server.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {DatabaseName}", server);
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = DatabaseName }
            .ConnectionString;
        var host = new MessagingHost(connectionString);
        // Boots the host: module migrations and Wolverine storage are created here.
        _ = host._factory.Server;

        await using var orders = host.CreateOrdersContext(tenantId: null);
        await orders.Database.ExecuteSqlRawAsync(orders.Database.GenerateCreateScript());
        await using var ledger = host.CreateLedgerContext(tenantId: null);
        await ledger.Database.ExecuteSqlRawAsync(ledger.Database.GenerateCreateScript());
        return host;
    }

    public HttpClient CreateClient(DevIdentity identity)
    {
        var token = Services.GetRequiredService<DevTokenIssuer>().CreateToken(identity, TimeProvider.System.GetUtcNow());
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    public ProbeOrdersDbContext CreateOrdersContext(Guid? tenantId)
    {
        var tenantContext = NewTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<ProbeOrdersDbContext>();
        options.UseModuleNpgsql(ConnectionString, ProbeOrdersDbContext.Schema)
            .AddInterceptors(TenantStampingInterceptor.Instance);
        return new ProbeOrdersDbContext(options.Options, tenantContext);
    }

    public ProbeLedgerDbContext CreateLedgerContext(Guid? tenantId)
    {
        var tenantContext = NewTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<ProbeLedgerDbContext>();
        options.UseModuleNpgsql(ConnectionString, ProbeLedgerDbContext.Schema)
            .AddInterceptors(TenantStampingInterceptor.Instance);
        return new ProbeLedgerDbContext(options.Options, tenantContext);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private static TenantContext NewTenantContext(Guid? tenantId)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id);
        }

        return tenantContext;
    }

    private sealed class MessagingFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:finguard", connectionString);
            builder.UseSetting("KeyVault:Uri", string.Empty);
            builder.UseSetting("APPLICATIONINSIGHTS_CONNECTION_STRING", string.Empty);
            builder.ConfigureServices(services =>
            {
                // Registered the way modules register theirs (singleton options, stateless interceptor).
                services.AddDbContext<ProbeOrdersDbContext>(
                    o => o.UseModuleNpgsql(connectionString, ProbeOrdersDbContext.Schema)
                        .AddInterceptors(TenantStampingInterceptor.Instance),
                    optionsLifetime: ServiceLifetime.Singleton);
                services.AddDbContext<ProbeLedgerDbContext>(
                    o => o.UseModuleNpgsql(connectionString, ProbeLedgerDbContext.Schema)
                        .AddInterceptors(TenantStampingInterceptor.Instance),
                    optionsLifetime: ServiceLifetime.Singleton);
                // Wolverine's documented DI extension point: no change to Program.cs.
                services.ConfigureWolverine(opts => opts.Discovery.IncludeAssembly(typeof(MessagingHost).Assembly));
                services.AddTransient<IStartupFilter, ProbeOrderStartupFilter>();
            });
        }
    }

    /// <summary>Test-only endpoint shaped like a real one: bind, send with the current tenant, no logic.</summary>
    private sealed class ProbeOrderStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints
                .MapPost(ProbeOrderRoute.Path, async (PlaceProbeOrderRequest request, IMessageBus bus, ITenantContext tenant,
                    CancellationToken ct) =>
                {
                    await bus.InvokeForCurrentTenantAsync(tenant, new PlaceProbeOrder(request.OrderId, request.Reference, false), ct);
                    return TypedResults.NoContent();
                })
                .RequireAuthorization(AuthorizationPolicies.TenantMember));
        };
    }
}

public static class ProbeOrderRoute
{
    public const string Path = "/api/_test/probe-orders";
}

public sealed record PlaceProbeOrderRequest(Guid OrderId, string Reference);
