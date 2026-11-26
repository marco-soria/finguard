namespace FinGuard.Advisory.Features.GetAdvisory;

/// <summary>The narrative of one evaluation, as plain-text paragraphs (D-253). The facts are not returned (D-256).</summary>
/// <param name="Narrator">Which narrator wrote it, for example <c>Template</c>.</param>
/// <param name="Paragraphs">Plain text; clients render them by interpolation only, never as HTML.</param>
public sealed record AdvisoryResponse(
    Guid EvaluationId, Guid SubmissionId, string Narrator, DateTimeOffset GeneratedAt, IReadOnlyList<string> Paragraphs);
