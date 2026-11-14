using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FinGuard.UnitTests.ServiceDefaults;

public sealed class CloudSeamsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("https://example.vault.azure.net/", true)]
    public void IsKeyVaultEnabled_UriSetting_EnabledOnlyWhenNotBlank(string? uri, bool expected)
    {
        var configuration = Build("KeyVault:Uri", uri);

        Assert.Equal(expected, CloudSeams.IsKeyVaultEnabled(configuration));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("InstrumentationKey=00000000-0000-0000-0000-000000000000", true)]
    public void IsAzureMonitorEnabled_ConnectionString_EnabledOnlyWhenNotBlank(string? connectionString, bool expected)
    {
        var configuration = Build("APPLICATIONINSIGHTS_CONNECTION_STRING", connectionString);

        Assert.Equal(expected, CloudSeams.IsAzureMonitorEnabled(configuration));
    }

    private static IConfiguration Build(string key, string? value) =>
        new ConfigurationBuilder().AddInMemoryCollection([new KeyValuePair<string, string?>(key, value)]).Build();
}
