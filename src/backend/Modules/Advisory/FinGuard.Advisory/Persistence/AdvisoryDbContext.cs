using FinGuard.Advisory.Features.Refinement;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FinGuard.Advisory.Persistence;

// Public for Wolverine's generated handler code (D-94); its entities stay internal.
public sealed class AdvisoryDbContext(DbContextOptions<AdvisoryDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string Schema = "advisory";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // One advisory per evaluation (D-255); each unique key also makes the consumer idempotent.
        modelBuilder.Entity<Advisory>(advisory =>
        {
            advisory.ToTable("advisories");
            advisory.HasKey(a => a.Id);
            advisory.Property(a => a.Id).ValueGeneratedNever();
            advisory.HasIndex(a => a.EvaluationId).IsUnique();
            advisory.HasIndex(a => a.SubmissionId).IsUnique();
            advisory.HasIndex(a => a.SourceEventId).IsUnique();
            advisory.Property(a => a.Facts).HasColumnType("jsonb");
            advisory.Property(a => a.Narrator).HasMaxLength(16);
            advisory.Property(a => a.RefinementStatus).HasMaxLength(16).HasDefaultValue(RefinementStatus.NotRequested);
            advisory.Property(a => a.RefinementReason).HasMaxLength(32);
            advisory.Property(a => a.ModelId).HasMaxLength(64);
            advisory.Property(a => a.AiContext);
        });

        // Last: the base class adds the tenant filter to the entity types mapped above.
        base.OnModelCreating(modelBuilder);
    }
}
