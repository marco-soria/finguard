namespace FinGuard.Portfolio.Contracts.Events;

// AssetClassTotal lives outside .Events: every type in an Events namespace is an integration event (ADR-0009).

/// <summary>
/// Published once per new accepted submission, in the same transaction that stores it (ADR-0009, ADR-0011). Never
/// published for rejected or duplicate uploads. An accepted submission is the current one for its cut-off date when
/// it is accepted (latest <see cref="ReceivedAt"/>, D-141).
/// </summary>
/// <param name="Totals">
/// Book value in USD per asset class (ADR-0010): exactly four entries, one per <see cref="AssetClassCode"/> in enum
/// order, zeros included.
/// </param>
/// <remarks>Additive changes only; a breaking change means a new event type (ADR-0011).</remarks>
public sealed record SubmissionAccepted(
    Guid EventId,
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid SubmissionId,
    DateOnly CutOffDate,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<AssetClassTotal> Totals)
{
    // Identifiers only: never dates or amounts in logs (D-77).
    public override string ToString() =>
        $"{nameof(SubmissionAccepted)} {{ EventId = {EventId}, SubmissionId = {SubmissionId} }}";
}
