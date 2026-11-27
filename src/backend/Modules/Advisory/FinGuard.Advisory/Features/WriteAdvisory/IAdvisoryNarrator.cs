namespace FinGuard.Advisory.Features.WriteAdvisory;

/// <summary>
/// The narrator port (ADR-0006): turns facts into plain-text English paragraphs separated by one blank line. A narrator
/// only explains; every number it writes must come from the facts' texts (<see cref="NarrativeValidator"/>).
/// </summary>
public interface IAdvisoryNarrator
{
    /// <summary>The model that writes the text, or null for a narrator without a model (the template).</summary>
    string? ModelId { get; }

    Task<string> NarrateAsync(AdvisoryFacts facts, CancellationToken ct);
}
