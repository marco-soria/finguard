using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FinGuard.Api.IntegrationTests.Tenancy;

/// <summary>Test-only tenant-aware context: proves the query filter and the interceptor against real PostgreSQL.</summary>
internal sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string Schema = "tenancy_probe";

    public DbSet<ProbeItem> Items => Set<ProbeItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<ProbeItem>().ToTable("probe_items");
        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class ProbeItem : ITenantOwned
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Marker { get; private set; } = string.Empty;

    public static ProbeItem Create(string marker, Guid tenantId = default) =>
        new() { Id = Guid.NewGuid(), Marker = marker, TenantId = tenantId };
}
