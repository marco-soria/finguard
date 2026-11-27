using FinGuard.Advisory.Features.WriteAdvisory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinGuard.Advisory.Infrastructure;

internal static class NarratorModelRegistration
{
    /// <summary>The Aspire model resource and connection string name (D-272 names the Azure deployment the same).</summary>
    public const string ConnectionName = "narrator";

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
        builder.Services.AddSingleton<IAdvisoryNarrator, AgentFrameworkNarrator>();
        return true;
    }
}
