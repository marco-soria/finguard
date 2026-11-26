using FinGuard.BuildingBlocks.Tenancy;

namespace FinGuard.Advisory.Persistence;

/// <summary>
/// The advisory of one evaluation (ADR-0014): the facts the narrator used and the narrative it wrote. A plain
/// persistence entity (aggregates exist only in Compliance, ADR-0007). Brief #19 may replace <see cref="Narrative"/>
/// and <see cref="Narrator"/> with validated model output; the facts never change.
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
