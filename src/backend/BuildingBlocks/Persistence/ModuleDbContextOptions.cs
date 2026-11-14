using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FinGuard.BuildingBlocks.Persistence;

public static class ModuleDbContextOptions
{
    /// <summary>Aspire resource name of the single FinGuard database.</summary>
    public const string ConnectionName = "finguard";

    // Runtime and design time both go through here so EF is configured identically.
    // Never turn on EF sensitive data logging or Npgsql parameter logging.
    public static DbContextOptionsBuilder UseModuleNpgsql(
        this DbContextOptionsBuilder options, string connectionString, string schema) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention();

    public static string GetRequiredFinGuardConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionName) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Connection string '{ConnectionName}' is missing. Run the app through the Aspire AppHost (aspire run).");
}

/// <summary>Placeholder used only by design-time DbContext factories; dotnet-ef never opens it.</summary>
public static class DesignTimeConnection
{
    public const string Placeholder = "Host=localhost;Database=finguard";
}
