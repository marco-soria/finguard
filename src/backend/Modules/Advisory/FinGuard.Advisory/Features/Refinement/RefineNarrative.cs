namespace FinGuard.Advisory.Features.Refinement;

/// <summary>
/// Asks the model for one advisory's context paragraph (ADR-0015, D-293); the template narrative never changes. Cascaded by <c>EvaluationCompletedHandler</c> when a
/// model is configured; handled outside any transaction on the <c>advisory-refinement</c> queue.
/// </summary>
public sealed record RefineNarrative(Guid AdvisoryId)
{
    // Identifiers only (D-162).
    public override string ToString() => $"{nameof(RefineNarrative)} {{ AdvisoryId = {AdvisoryId} }}";
}

/// <summary>The result of one model call: the validated context paragraph or the reason it was rejected.</summary>
/// <param name="AiContext">Set only when <paramref name="Outcome"/> is <see cref="RefinementOutcome.Accepted"/>.</param>
/// <param name="ModelId">The model that answered; set only when accepted.</param>
public sealed record StoreRefinedNarrative(Guid AdvisoryId, RefinementOutcome Outcome, string? AiContext, string? ModelId)
{
    // Identifiers and the outcome code only: never the text (D-162). The payload itself sits in Wolverine's
    // envelope tables like every other message (R-35).
    public override string ToString() =>
        $"{nameof(StoreRefinedNarrative)} {{ AdvisoryId = {AdvisoryId}, Outcome = {Outcome} }}";
}

/// <summary>Accepted, or the reason code stored in <c>refinement_reason</c>.</summary>
public enum RefinementOutcome
{
    Accepted,
    Timeout,
    ModelError,
    Digits,
    Symbols,
    StatusWording,
    Format,
    Length,
}

/// <summary>The values of <c>advisory.advisories.refinement_status</c>.</summary>
public static class RefinementStatus
{
    public const string NotRequested = nameof(NotRequested);
    public const string Pending = nameof(Pending);
    public const string Accepted = nameof(Accepted);
    public const string Rejected = nameof(Rejected);
}
