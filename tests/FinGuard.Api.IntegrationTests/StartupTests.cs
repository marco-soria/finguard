using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FinGuard.Api.IntegrationTests;

public sealed class StartupTests(ApiFixture fixture)
{
    [Theory]
    [InlineData("tenancy")]
    [InlineData("portfolio")]
    [InlineData("compliance")]
    [InlineData("advisory")]
    public async Task Startup_Development_AppliesInitialSchemaPerModule(string schema)
    {
        // EFCore.NamingConventions also snake-cases the history table columns.
        var migrations = await QueryAsync<string>($"SELECT migration_id FROM \"{schema}\".__ef_migrations_history");

        Assert.EndsWith("_InitialSchema", migrations.Order(StringComparer.Ordinal).First(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_Always_CreatesWolverineStorage()
    {
        var tables = await QueryAsync<string>(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'wolverine'");

        Assert.Contains("wolverine_incoming_envelopes", tables);
        Assert.Contains("wolverine_outgoing_envelopes", tables);
    }

    [Fact]
    public void Startup_WithoutCloudSettings_DisablesBothSeams()
    {
        var configuration = (IConfigurationRoot)fixture.Factory.Services.GetRequiredService<IConfiguration>();

        var seamMessages = fixture.Logs.GetSnapshot()
            .Where(r => r.Category == "Microsoft.Extensions.Hosting.CloudSeamsStartupLogger")
            .Select(r => r.Message)
            .ToList();

        Assert.DoesNotContain(configuration.Providers, p => p.GetType().FullName!.Contains("KeyVault", StringComparison.Ordinal));
        Assert.Contains("Cloud seam Key Vault configuration: disabled", seamMessages);
        Assert.Contains("Cloud seam Azure Monitor exporter: disabled", seamMessages);
    }

    // Read-only queries; the schema name comes from the InlineData above, never from user input.
    private async Task<List<T>> QueryAsync<T>(string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(reader.GetFieldValue<T>(0));
        }

        return rows;
    }
}
