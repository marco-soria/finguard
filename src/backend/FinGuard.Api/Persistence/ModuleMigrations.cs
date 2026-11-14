using Microsoft.EntityFrameworkCore;

namespace FinGuard.Api.Persistence;

internal static partial class ModuleMigrations
{
    // Development only (D-31). The production migration strategy is decided in Phase 5.
    public static async Task ApplyModuleMigrationsAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ModuleMigrations));

        foreach (var context in scope.ServiceProvider.GetServices<DbContext>())
        {
            await context.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
            LogMigrated(logger, context.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied migrations for {DbContext}")]
    private static partial void LogMigrated(ILogger logger, string dbContext);
}
