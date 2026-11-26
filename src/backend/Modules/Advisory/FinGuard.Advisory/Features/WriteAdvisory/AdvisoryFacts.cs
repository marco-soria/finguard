using FinGuard.Compliance.Contracts;

namespace FinGuard.Advisory.Features.WriteAdvisory;

/// <summary>
/// Everything a narrator may say about one evaluation (ADR-0014): the event's values plus their display texts,
/// formatted once by <see cref="AdvisoryFormat"/>. Narrators copy these texts; they never compute or format numbers.
/// There is no free text: every field comes from <c>EvaluationCompleted</c> (ADR-0002).
/// </summary>
/// <param name="CutOffDateText">ISO date, as the SPA shows it (<c>2026-06-30</c>).</param>
/// <param name="TargetRatioText">The rebalance target ratio as a percentage (<c>85.00 %</c>).</param>
/// <param name="Classes">Four entries in <see cref="AssetClassCode"/> order.</param>
public sealed record AdvisoryFacts(
    Guid EvaluationId,
    Guid SubmissionId,
    DateOnly CutOffDate,
    string CutOffDateText,
    ComplianceStatusCode OverallStatus,
    int PolicyVersion,
    string PolicyVersionText,
    ProposalStatusCode ProposalStatus,
    decimal AlertThreshold,
    decimal RebalanceTargetRatio,
    string TargetRatioText,
    IReadOnlyList<ClassFacts> Classes)
{
    /// <summary>Every display text: the only source of numbers a narrative may contain (D-254).</summary>
    public IEnumerable<string> Texts() =>
        new[] { CutOffDateText, PolicyVersionText, TargetRatioText }.Concat(Classes.SelectMany(c => c.Texts()));

    // Identifiers only (D-162).
    public override string ToString() =>
        $"{nameof(AdvisoryFacts)} {{ EvaluationId = {EvaluationId}, SubmissionId = {SubmissionId} }}";
}

/// <summary>One asset class: raw values from the event and their display texts.</summary>
/// <param name="Name">Display name as in the SPA (<c>Fixed income</c>).</param>
/// <param name="Share">Book value / total, unrounded.</param>
/// <param name="Change">Proposed minus current book value; negative for a reduction.</param>
/// <param name="ChangeText">The absolute change as money; the sentence says whether it is a reduction.</param>
public sealed record ClassFacts(
    AssetClassCode AssetClass,
    string Name,
    ComplianceStatusCode Status,
    decimal BookValue,
    decimal Share,
    decimal Limit,
    decimal Utilization,
    decimal ProposedBookValue,
    decimal Change,
    string BookValueText,
    string ShareText,
    string LimitText,
    string UtilizationText,
    string ProposedText,
    string ChangeText)
{
    public IEnumerable<string> Texts() =>
        [BookValueText, ShareText, LimitText, UtilizationText, ProposedText, ChangeText];

    // The class only (D-162).
    public override string ToString() => $"{nameof(ClassFacts)} {{ AssetClass = {AssetClass} }}";
}
