namespace FinGuard.Advisory.Features.WriteAdvisory;

/// <summary>
/// The narrator port (ADR-0006): turns facts into plain-text English. A narrator only explains. The template writes
/// the narrative, every number from the facts' texts (<see cref="NarrativeValidator"/>); the model narrator writes
/// one context paragraph with no figures or statuses (<c>AiContextValidator</c>, D-293).
/// </summary>
public interface IAdvisoryNarrator
{
    /// <summary>The model that writes the text, or null for a narrator without a model (the template).</summary>
    string? ModelId { get; }

    Task<string> NarrateAsync(AdvisoryFacts facts, CancellationToken ct);
}
