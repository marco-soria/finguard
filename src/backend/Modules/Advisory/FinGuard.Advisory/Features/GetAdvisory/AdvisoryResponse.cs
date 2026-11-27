namespace FinGuard.Advisory.Features.GetAdvisory;

/// <summary>The narrative of one evaluation, as plain-text paragraphs (D-253). The facts are not returned (D-256).</summary>
/// <param name="Narrator">Which narrator wrote it: always <c>Template</c> (D-293).</param>
/// <param name="Paragraphs">Plain text; clients render them by interpolation only, never as HTML.</param>
/// <param name="Model">The model that wrote <paramref name="AiContext"/>; null otherwise (ADR-0015).</param>
/// <param name="Refinement"><c>NotRequested</c>, <c>Pending</c>, <c>Accepted</c> or <c>Rejected</c>.</param>
/// <param name="AiContext">One plain-text paragraph with no figures or statuses (D-293); null unless accepted.</param>
public sealed record AdvisoryResponse(
    Guid EvaluationId, Guid SubmissionId, string Narrator, DateTimeOffset GeneratedAt, IReadOnlyList<string> Paragraphs,
    string? Model, string Refinement, string? AiContext);
