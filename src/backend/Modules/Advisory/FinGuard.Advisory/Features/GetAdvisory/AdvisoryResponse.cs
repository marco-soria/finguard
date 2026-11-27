namespace FinGuard.Advisory.Features.GetAdvisory;

/// <summary>The narrative of one evaluation, as plain-text paragraphs (D-253). The facts are not returned (D-256).</summary>
/// <param name="Narrator">Which narrator wrote it: <c>Template</c> or <c>Model</c>.</param>
/// <param name="Paragraphs">Plain text; clients render them by interpolation only, never as HTML.</param>
/// <param name="Model">The model that wrote it; null for the template (ADR-0015).</param>
/// <param name="Refinement"><c>NotRequested</c>, <c>Pending</c>, <c>Accepted</c> or <c>Rejected</c>.</param>
public sealed record AdvisoryResponse(
    Guid EvaluationId, Guid SubmissionId, string Narrator, DateTimeOffset GeneratedAt, IReadOnlyList<string> Paragraphs,
    string? Model, string Refinement);
