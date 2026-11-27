using FinGuard.Advisory.Features.WriteAdvisory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinGuard.Advisory.Infrastructure;

internal static class NarratorModelRegistration
{
    /// <summary>The Aspire model resource and connection string name (D-272 names the Azure deployment the same).</summary>
    public const string ConnectionName = "narrator";

    // CommunityToolkit.Aspire.OllamaSharp 13.6.1 names its HttpClient "<connection name>_httpClient".
    private const string OllamaHttpClientSuffix = "_httpClient";

    /// <summary>
    /// Registers the Ollama chat client and <see cref="AgentFrameworkNarrator"/> only when the <c>narrator</c> connection
    /// string exists (ADR-0015); returns whether it did. Without it the app writes template narratives only.
    /// </summary>
    public static bool AddNarratorModel(this IHostApplicationBuilder builder)
    {
        if (string.IsNullOrEmpty(builder.Configuration.GetConnectionString(ConnectionName)))
        {
            return false;
        }

        // No health check: the Api must stay healthy while the model downloads or is stopped.
        builder.AddOllamaApiClient(ConnectionName, settings => settings.DisableHealthChecks = true)
            // Traces keep model, timings and token counts, never prompts or completions: they contain amounts (D-287).
            .AddChatClient(telemetry => telemetry.EnableSensitiveData = false);
        // ServiceDefaults gives every HttpClient the standard resilience handler (10 s per attempt, retries): it cut local
        // model calls at 10 s and sent them again. The narrator's own timeout governs the call; there is no retry (D-282).
        // EXTEXP0001: the documented way to drop handlers added by ConfigureHttpClientDefaults; re-check on each upgrade
        // (D-292, NarratorHttpClientTests pins the result).
#pragma warning disable EXTEXP0001
        builder.Services.AddHttpClient(ConnectionName + OllamaHttpClientSuffix)
            .ConfigureHttpClient(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
        builder.Services.AddSingleton<IAdvisoryNarrator, AgentFrameworkNarrator>();
        return true;
    }
}
