using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FinGuard.Api.IntegrationTests.Messaging;

// Test-only probe modules (D-65): two tenant-aware DbContexts in their own schemas, standing in for two real modules.
// Public because Wolverine discovers and generates code only for public handler and message types.

public sealed class ProbeOrdersDbContext(DbContextOptions<ProbeOrdersDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string Schema = "probe_orders";

    public DbSet<ProbeOrder> Orders => Set<ProbeOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<ProbeOrder>().ToTable("probe_orders");
        base.OnModelCreating(modelBuilder);
    }
}

public sealed class ProbeLedgerDbContext(DbContextOptions<ProbeLedgerDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string Schema = "probe_ledger";

    public DbSet<ProbeLedgerEntry> Entries => Set<ProbeLedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<ProbeLedgerEntry>(entry =>
        {
            entry.ToTable("probe_ledger_entries");
            // Natural idempotency (D-69): one ledger entry per source event.
            entry.HasIndex(e => e.SourceEventId).IsUnique();
        });
        base.OnModelCreating(modelBuilder);
    }
}

public sealed class ProbeOrder : ITenantOwned
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Reference { get; private set; } = string.Empty;

    public static ProbeOrder Create(Guid id, string reference) => new() { Id = id, Reference = reference };
}

public sealed class ProbeLedgerEntry : ITenantOwned
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SourceEventId { get; private set; }

    public string Reference { get; private set; } = string.Empty;

    public static ProbeLedgerEntry Create(Guid sourceEventId, string reference) =>
        new() { Id = Guid.NewGuid(), SourceEventId = sourceEventId, Reference = reference };
}
