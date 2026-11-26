namespace FinGuard.Advisory.Features.WriteAdvisory;

/// <summary>
/// The narrator port (ADR-0006): turns facts into plain-text English paragraphs separated by one blank line. A narrator
/// only explains; every number it writes must come from the facts' texts (<see cref="NarrativeValidator"/>).
/// </summary>
public interface IAdvisoryNarrator
{
    Task<string> NarrateAsync(AdvisoryFacts facts, CancellationToken ct);
}
