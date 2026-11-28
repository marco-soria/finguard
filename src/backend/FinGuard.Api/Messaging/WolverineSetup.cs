using FinGuard.Advisory;
using FinGuard.BuildingBlocks.Messaging;
using FinGuard.BuildingBlocks.Tenancy;
using FinGuard.Compliance;
using FinGuard.Portfolio;
using FinGuard.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

namespace FinGuard.Api.Messaging;

/// <summary>Wolverine configuration for module messaging (ADR-0003, ADR-0009).</summary>
internal static class WolverineSetup
{
    public static void AddFinGuardMessaging(this WebApplicationBuilder builder) =>
        builder.Host.UseWolverine(opts =>
        {
            // One main envelope store in schema `wolverine` (D-29, D-66). Module DbContexts share the database, so
            // outbox rows are written on the DbContext's connection and transaction. Tables are created on startup by
            // the default AutoBuildMessageStorageOnStartup = CreateOrDefault.
            opts.PersistMessagesWithPostgresqlUsingDataSourceFromContainer(schemaName: "wolverine");
            opts.UseRuntimeCompilation();
            opts.Discovery.IncludeAssembly(typeof(TenancyModule).Assembly);
            opts.Discovery.IncludeAssembly(typeof(PortfolioModule).Assembly);
            opts.Discovery.IncludeAssembly(typeof(ComplianceModule).Assembly);
            opts.Discovery.IncludeAssembly(typeof(AdvisoryModule).Assembly);

            // Eager mode (default): the transaction opens before the handler runs (D-67). EF retries are off in every
            // module registration because Eager mode refuses EnableRetryOnFailure.
            opts.UseEntityFrameworkCoreTransactions();
            opts.Policies.AutoApplyTransactions();
            // Cascaded events survive a crash between commit and handling (D-71).
            opts.Policies.UseDurableLocalQueues();
            opts.Policies.AddMiddleware(typeof(TenantPropagationMiddleware));
            opts.AddAdvisoryMessaging();
            // Module DbContexts registered through Aspire's EnrichNpgsqlDbContext are resolved from the message scope
            // (service location). ITenantContext must come from that same scope, or the middleware sets the tenant on
            // an inlined `new TenantContext()` the DbContext never sees and every write fails closed (D-95).
            opts.CodeGeneration.AlwaysUseServiceLocationFor<ITenantContext>();

            // R-21 (D-77): success is noise at Information; start/finish stays at its Debug default. Failures stay at
            // Error and print the message's ToString(), which every message type overrides with identifiers only.
            opts.Policies.MessageSuccessLogLevel(LogLevel.Debug);
            opts.Policies.MessageExecutionLogLevel(LogLevel.Debug);

            // Retries the whole handler, transaction included (R-20: Dapper queries get no automatic retry).
            opts.Policies.OnException<NpgsqlException>(e => e.IsTransient)
                .RetryWithCooldown(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1))
                .Then.MoveToErrorQueue();

            // Optimistic concurrency (xmin, D-317): the retry runs the handler again on a fresh DbContext, which reloads the
            // row and finds it already updated. Global, not per module: only Advisory has a concurrency token today.
            opts.Policies.OnException<DbUpdateConcurrencyException>()
                .RetryWithCooldown(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1))
                .Then.MoveToErrorQueue();

            // Solo is Development-only: multi-replica environments (Phase 5) need Balanced mode.
            if (builder.Environment.IsDevelopment())
            {
                opts.Durability.Mode = DurabilityMode.Solo;
            }
        });
}
