using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

// Cloud seams (ADR-0005): each Azure integration is on only when its setting is present.
public static class CloudSeams
{
    public static bool IsKeyVaultEnabled(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["KeyVault:Uri"]);

    public static bool IsAzureMonitorEnabled(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]);
}
