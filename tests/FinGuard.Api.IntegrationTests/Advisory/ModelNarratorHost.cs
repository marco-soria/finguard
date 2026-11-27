using System.Net.Http.Headers;
using FinGuard.Api.DevAuth;
using FinGuard.Tenancy.Development;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace FinGuard.Api.IntegrationTests.Advisory;

/// <summary>
/// The Api with a model narrator configured (ADR-0015): a <c>narrator</c> connection string, so Advisory registers
/// <c>AgentFrameworkNarrator</c>, and a fake <see cref="IChatClient"/> in place of Ollama (tests never call a real
/// model). Own database in the shared container, so its Wolverine node never takes the shared host's messages.
/// </summary>
public sealed class ModelNarratorHost(ApiFixture fixture) : IAsyncLifetime
{
    // One database per host instance: each test class that uses this fixture gets its own.
    private readonly string _databaseName = $"finguard_model_{Guid.NewGuid():N}";

    public const string ModelId = "fake-model";

    public ScriptedChatClient ChatClient { get; } = new();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await using (var server = new NpgsqlConnection(fixture.ConnectionString))
        {
            await server.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {_databaseName}", server);
            await create.ExecuteNonQueryAsync();
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = _databaseName }
            .ConnectionString;
        Factory = new ApiFixture.ApiFactory(ConnectionString, builder =>
        {
            // Never contacted: the fake chat client replaces the Ollama one.
            builder.UseSetting("ConnectionStrings:narrator", $"Endpoint=http://127.0.0.1:9;Model={ModelId}");
            builder.UseSetting("Advisory:Narrator:TimeoutSeconds", "1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatClient>();
                services.AddSingleton<IChatClient>(ChatClient);
            });
        });
        _ = Factory.Server;
    }

    public HttpClient CreateClient(DevIdentity identity)
    {
        var token = Factory.Services.GetRequiredService<DevTokenIssuer>()
            .CreateToken(identity, TimeProvider.System.GetUtcNow());
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }
    }
}

/// <summary>A fake model whose answer each test sets. Tests in one class run one at a time.</summary>
public sealed class ScriptedChatClient : IChatClient
{
    public Func<CancellationToken, Task<string>> Respond { get; set; } = _ => Task.FromResult(string.Empty);

    public int Calls { get; private set; }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls++;
        var text = await Respond(cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text)) { ModelId = ModelNarratorHost.ModelId };
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The narrator does not stream.");

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata("fake", null, ModelNarratorHost.ModelId)
        : serviceType.IsInstanceOfType(this) ? this
        : null;

    public void Dispose()
    {
    }
}
