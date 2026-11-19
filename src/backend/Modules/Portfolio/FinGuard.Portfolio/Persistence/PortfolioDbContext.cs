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

        modelBuilder.Entity<Position>(position =>
        {
            position.ToTable("positions");
            position.HasKey(p => new { p.SubmissionId, p.RowNumber });
            position.Property(p => p.PositionId).IsRequired().HasMaxLength(Position.PositionIdMaxLength);
            position.Property(p => p.AssetClass)
                .HasConversion(a => a.ToCode(), code => AssetClassCodes.FromCode(code))
                .HasMaxLength(AssetClassCodes.MaxLength);
            position.Property(p => p.MarketValueUsd).HasPrecision(18, 2);
            position.HasOne<Submission>().WithMany().HasForeignKey(p => p.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubmissionError>(error =>
        {
            error.ToTable("submission_errors");
            error.HasKey(e => new { e.SubmissionId, e.Ordinal });
            error.Property(e => e.Column).HasMaxLength(SubmissionError.ColumnMaxLength);
            error.Property(e => e.Code).HasMaxLength(SubmissionError.CodeMaxLength);
            error.Property(e => e.Message).HasMaxLength(SubmissionError.MessageMaxLength);
            error.HasOne<Submission>().WithMany().HasForeignKey(e => e.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        });

        // Last: the base class adds the tenant filter to the entity types mapped above.
        base.OnModelCreating(modelBuilder);
    }
}
