using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FinGuard.Compliance.Persistence;

// Public for Wolverine's generated handler code (D-94); its entities stay internal.
public sealed class ComplianceDbContext(DbContextOptions<ComplianceDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string Schema = "compliance";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<PortfolioSnapshot>(snapshot =>
        {
            snapshot.ToTable("portfolio_snapshots");
            snapshot.HasKey(s => s.SubmissionId);
            snapshot.Property(s => s.SubmissionId).ValueGeneratedNever();
            snapshot.Property(s => s.Cash).HasPrecision(18, 2);
            snapshot.Property(s => s.FixedIncome).HasPrecision(18, 2);
            snapshot.Property(s => s.Shares).HasPrecision(18, 2);
            snapshot.Property(s => s.RealEstate).HasPrecision(18, 2);
            // Idempotent consumer (ADR-0009): one row per source event.
            snapshot.HasIndex(s => s.SourceEventId).IsUnique();
            // Phase 3 reads the current snapshot per cut-off date: latest received_at (D-141).
            snapshot.HasIndex(s => new { s.TenantId, s.CutOffDate, s.ReceivedAt });
        });

        // Last: the base class adds the tenant filter to the entity types mapped above.
        base.OnModelCreating(modelBuilder);
    }
}
