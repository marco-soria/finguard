using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FinGuard.Portfolio.Persistence;

public sealed class PortfolioDbContext(DbContextOptions<PortfolioDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext(options, tenantContext)
{
    public const string Schema = "portfolio";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Submission>(submission =>
        {
            submission.ToTable("submissions");
            submission.Property(s => s.FileName).HasMaxLength(Submission.FileNameMaxLength);
            submission.Property(s => s.Sha256).HasMaxLength(Submission.Sha256Length).IsFixedLength();
            submission.Property(s => s.Status).HasConversion<string>().HasMaxLength(Submission.StatusMaxLength);
            // History query of Brief #9: a tenant's submissions by cut-off date, newest first.
            submission.HasIndex(s => new { s.TenantId, s.CutOffDate, s.ReceivedAt });
        });

        modelBuilder.Entity<SubmissionContent>(content =>
        {
            content.ToTable("submission_contents");
            content.HasKey(c => c.SubmissionId);
            content.HasOne<Submission>().WithOne().HasForeignKey<SubmissionContent>(c => c.SubmissionId);
        });

        // Last: the base class adds the tenant filter to the entity types mapped above.
        base.OnModelCreating(modelBuilder);
    }
}
