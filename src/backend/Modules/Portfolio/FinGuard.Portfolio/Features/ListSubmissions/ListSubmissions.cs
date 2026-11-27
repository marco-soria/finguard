namespace FinGuard.Portfolio.Features.ListSubmissions;

/// <summary>
/// One page of the envelope's tenant's submissions (ADR-0009, D-299, D-304). Raw query-string values: the handler
/// validates them, so every malformed value answers with a Portfolio code instead of a binding failure.
/// </summary>
public sealed record ListSubmissions(
    string? Limit = null,
    string? Cursor = null,
    string? Status = null,
    string? CutOffFrom = null,
    string? CutOffTo = null,
    string? CurrentOnly = null)
{
    // Identifiers only in logs (R-21, D-77): the filters carry dates and statuses, so none of them is printed.
    public override string ToString() => $"{nameof(ListSubmissions)} {{ }}";
}
