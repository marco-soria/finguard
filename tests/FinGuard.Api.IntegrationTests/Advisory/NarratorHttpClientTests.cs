using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace FinGuard.Api.IntegrationTests.Advisory;

/// <summary>
/// The narrator's own timeout governs the model call (ADR-0015). ServiceDefaults adds the standard resilience handler to
/// every HttpClient (10 s per attempt, retries, 30 s in total), which cut local model calls at 10 s and sent them again;
/// the Ollama client must run without it.
/// </summary>
public sealed class NarratorHttpClientTests(ModelNarratorHost host) : IClassFixture<ModelNarratorHost>
{
    [Fact]
    public void OllamaClient_HttpClient_HasNoResilienceHandler()
    {
        // The HttpClient the registered Ollama client really uses, not one looked up by name: a name the integration
        // does not use would pass a by-name check (D-292).
        var ollama = (OllamaApiClient)host.Factory.Services.GetRequiredService<IOllamaApiClient>();
        var http = (HttpClient)typeof(OllamaApiClient).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(ollama)!;
        var handler = (HttpMessageHandler)typeof(HttpMessageInvoker)
            .GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(http)!;

        var chain = new List<string>();
        for (HttpMessageHandler? h = handler; h is not null; h = (h as DelegatingHandler)?.InnerHandler)
        {
            chain.Add(h.GetType().FullName!);
        }

        Assert.Contains(chain, name => name.EndsWith("SocketsHttpHandler", StringComparison.Ordinal));
        Assert.DoesNotContain(chain, name => name.Contains("Resilience", StringComparison.Ordinal));
    }
}
