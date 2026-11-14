using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

// Logs once at startup whether each cloud seam is on. Never logs URIs or connection strings.
internal sealed partial class CloudSeamsStartupLogger(
    ILogger<CloudSeamsStartupLogger> logger, bool keyVaultEnabled, bool azureMonitorEnabled) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        LogSeam(logger, "Key Vault configuration", keyVaultEnabled ? "enabled" : "disabled");
        LogSeam(logger, "Azure Monitor exporter", azureMonitorEnabled ? "enabled" : "disabled");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Cloud seam {Seam}: {State}")]
    private static partial void LogSeam(ILogger logger, string seam, string state);
}
