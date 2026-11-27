using FinGuard.Advisory.Features.Refinement;
using FinGuard.BuildingBlocks.Tenancy;

namespace FinGuard.Advisory.Persistence;

/// <summary>
/// The advisory of one evaluation (ADR-0014): the facts the narrator used and the narrative it wrote. A plain
/// persistence entity (aggregates exist only in Compliance, ADR-0007). The narrative and the facts never change; a
/// validated model paragraph may be added once as <see cref="AiContext"/>, while the refinement is pending (D-293).
/// </summary>
internal sealed class Advisory : ITenantOwned
{
    private Advisory()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid EvaluationId { get; private set; }

    public Guid SubmissionId { get; private set; }

    /// <summary>The <c>EvaluationCompleted.EventId</c> it came from; unique, so a redelivery cannot add a second row.</summary>
    public Guid SourceEventId { get; private set; }

    /// <summary><c>AdvisoryFacts</c> as JSON (<c>jsonb</c>), read only as a whole.</summary>
    public string Facts { get; private set; } = string.Empty;

    /// <summary>Plain-text paragraphs separated by one blank line.</summary>
    public string Narrative { get; private set; } = string.Empty;

    /// <summary>Which narrator wrote <see cref="Narrative"/>, for example <c>Template</c>.</summary>
    public string Narrator { get; private set; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; private set; }

    /// <summary>One of <see cref="Features.Refinement.RefinementStatus"/>.</summary>
    public string RefinementStatus { get; private set; } = Features.Refinement.RefinementStatus.NotRequested;

    /// <summary>The <see cref="RefinementOutcome"/> that rejected the model text; null otherwise.</summary>
    public string? RefinementReason { get; private set; }

    /// <summary>The model that wrote <see cref="AiContext"/>; null otherwise.</summary>
    public string? ModelId { get; private set; }

    /// <summary>One plain-text paragraph of context with no figures or statuses (D-293); null unless accepted.</summary>
    public string? AiContext { get; private set; }

    public void RequestRefinement() => RefinementStatus = Features.Refinement.RefinementStatus.Pending;

    /// <summary>
    /// Applies a refinement outcome only while pending, so a redelivered message changes nothing. Returns whether it
    /// applied.
    /// </summary>
    public bool ApplyRefinement(RefinementOutcome outcome, string? aiContext, string? modelId)
    {
        if (RefinementStatus != Features.Refinement.RefinementStatus.Pending)
        {
            return false;
        }

        if (outcome == RefinementOutcome.Accepted)
        {
            ArgumentException.ThrowIfNullOrEmpty(aiContext);
            ArgumentException.ThrowIfNullOrEmpty(modelId);
            AiContext = aiContext;
            ModelId = modelId;
            RefinementStatus = Features.Refinement.RefinementStatus.Accepted;
        }
        else
        {
            RefinementStatus = Features.Refinement.RefinementStatus.Rejected;
            RefinementReason = outcome.ToString();
        }

        return true;
    }

    // TenantId stays empty: TenantStampingInterceptor sets it from ITenantContext on save (ADR-0004).
    public static Advisory Create(
        Guid evaluationId, Guid submissionId, Guid sourceEventId, string facts, string narrative, string narrator,
        DateTimeOffset generatedAt) =>
        new()
        {
            Id = Guid.CreateVersion7(generatedAt),
            EvaluationId = evaluationId,
            SubmissionId = submissionId,
            SourceEventId = sourceEventId,
            Facts = facts,
            Narrative = narrative,
            Narrator = narrator,
            GeneratedAt = generatedAt,
        };
}
