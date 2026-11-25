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

        // Global, not ITenantOwned (D-175): the base class adds no tenant filter. CHECK constraints mirror the
        // LimitPolicy invariants; the repository still maps through LimitPolicy.Create as a second line of defence.
        modelBuilder.Entity<LimitPolicyRow>(policy =>
        {
            policy.ToTable("limit_policies", t =>
            {
                t.HasCheckConstraint("ck_limit_policies_cash", "cash > 0 AND cash <= 1");
                t.HasCheckConstraint("ck_limit_policies_fixed_income", "fixed_income > 0 AND fixed_income <= 1");
                t.HasCheckConstraint("ck_limit_policies_shares", "shares > 0 AND shares <= 1");
                t.HasCheckConstraint("ck_limit_policies_real_estate", "real_estate > 0 AND real_estate <= 1");
                t.HasCheckConstraint(
                    "ck_limit_policies_alert_threshold", "alert_threshold > 0 AND alert_threshold <= 1");
                t.HasCheckConstraint(
                    "ck_limit_policies_rebalance_target_ratio",
                    "rebalance_target_ratio > 0 AND rebalance_target_ratio < alert_threshold");
                t.HasCheckConstraint(
                    "ck_limit_policies_limits_cover_target",
                    "rebalance_target_ratio * (cash + fixed_income + shares + real_estate) >= 1");
            });
            policy.HasKey(p => p.Version);
            policy.Property(p => p.Version).ValueGeneratedNever();
            policy.HasIndex(p => p.EffectiveFrom).IsUnique();
            policy.Property(p => p.Cash).HasPrecision(5, 4);
            policy.Property(p => p.FixedIncome).HasPrecision(5, 4);
            policy.Property(p => p.Shares).HasPrecision(5, 4);
            policy.Property(p => p.RealEstate).HasPrecision(5, 4);
            policy.Property(p => p.AlertThreshold).HasPrecision(5, 4);
            policy.Property(p => p.RebalanceTargetRatio).HasPrecision(5, 4);
            // Seed v1 (AGENTS.md), same rows in every environment. Effective from 2000-01-01 so realistic dates have a policy.
            policy.HasData(new LimitPolicyRow
            {
                Version = 1,
                EffectiveFrom = new DateOnly(2000, 1, 1),
                Cash = 0.40m,
                FixedIncome = 0.30m,
                Shares = 0.25m,
                RealEstate = 0.25m,
                AlertThreshold = 0.90m,
                RebalanceTargetRatio = 0.85m,
                CreatedAt = new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero),
            });
        });

        // One evaluation per submission (D-193). Statuses and classes are stored by enum name; share and utilization
        // are unconstrained numeric so the stored value equals the domain's unrounded decimal (D-195).
        modelBuilder.Entity<EvaluationRow>(evaluation =>
        {
            evaluation.ToTable("evaluations");
            evaluation.HasKey(e => e.Id);
            evaluation.Property(e => e.Id).ValueGeneratedNever();
            evaluation.HasIndex(e => e.SubmissionId).IsUnique();
            evaluation.HasIndex(e => new { e.TenantId, e.CutOffDate });
            evaluation.Property(e => e.TotalBookValue).HasPrecision(18, 2);
            evaluation.Property(e => e.OverallStatus).HasConversion<string>().HasMaxLength(16);
            evaluation.Property(e => e.CashLimit).HasPrecision(5, 4);
            evaluation.Property(e => e.FixedIncomeLimit).HasPrecision(5, 4);
            evaluation.Property(e => e.SharesLimit).HasPrecision(5, 4);
            evaluation.Property(e => e.RealEstateLimit).HasPrecision(5, 4);
            evaluation.Property(e => e.AlertThreshold).HasPrecision(5, 4);
            evaluation.Property(e => e.RebalanceTargetRatio).HasPrecision(5, 4);
            evaluation.Property(e => e.ProposalStatus).HasConversion<string>().HasMaxLength(16);
            evaluation.HasMany(e => e.Classes).WithOne().HasForeignKey(c => c.EvaluationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EvaluationClassRow>(evaluationClass =>
        {
            evaluationClass.ToTable("evaluation_classes");
            evaluationClass.HasKey(c => new { c.EvaluationId, c.AssetClass });
            evaluationClass.Property(c => c.AssetClass).HasConversion<string>().HasMaxLength(16);
            evaluationClass.Property(c => c.BookValue).HasPrecision(18, 2);
            evaluationClass.Property(c => c.Share).HasColumnType("numeric");
            evaluationClass.Property(c => c.Utilization).HasColumnType("numeric");
            evaluationClass.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
            evaluationClass.Property(c => c.ProposedBookValue).HasPrecision(18, 2);
        });

        // Last: the base class adds the tenant filter to the entity types mapped above.
        base.OnModelCreating(modelBuilder);
    }
}
