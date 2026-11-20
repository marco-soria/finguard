using FinGuard.BuildingBlocks.Tenancy;

namespace FinGuard.Compliance.Persistence;

/// <summary>
/// Compliance's own copy of an accepted submission's class totals (D-157), stored from <c>SubmissionAccepted</c>.
/// A persistence entity, not a domain aggregate: Phase 3 builds the evaluation from it. Book value in USD (ADR-0010).
/// </summary>
internal sealed class PortfolioSnapshot : ITenantOwned
{
    private PortfolioSnapshot()
    {
    }

    public Guid SubmissionId { get; private set; }

    public Guid TenantId { get; private set; }

    public DateOnly CutOffDate { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public decimal Cash { get; private set; }

    public decimal FixedIncome { get; private set; }

    public decimal Shares { get; private set; }

    public decimal RealEstate { get; private set; }

    /// <summary>The <c>SubmissionAccepted.EventId</c> it came from; unique, so a redelivery cannot add a second row.</summary>
    public Guid SourceEventId { get; private set; }

    public DateTimeOffset StoredAt { get; private set; }

    // TenantId stays empty: TenantStampingInterceptor sets it from ITenantContext on save (ADR-0004).
    public static PortfolioSnapshot Create(
        Guid submissionId, DateOnly cutOffDate, DateTimeOffset receivedAt, decimal cash, decimal fixedIncome,
        decimal shares, decimal realEstate, Guid sourceEventId, DateTimeOffset storedAt) =>
        new()
        {
            SubmissionId = submissionId,
            CutOffDate = cutOffDate,
            ReceivedAt = receivedAt,
            Cash = cash,
            FixedIncome = fixedIncome,
            Shares = shares,
            RealEstate = realEstate,
            SourceEventId = sourceEventId,
            StoredAt = storedAt,
        };
}
