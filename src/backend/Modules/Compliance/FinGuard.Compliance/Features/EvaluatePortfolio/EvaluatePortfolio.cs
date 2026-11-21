namespace FinGuard.Compliance.Features.EvaluatePortfolio;

/// <summary>
/// Internal command, cascaded by <c>SubmissionAcceptedHandler</c> after it stores the snapshot (D-192). Not a contract:
/// the tenant travels on the envelope (ADR-0009).
/// </summary>
public sealed record EvaluatePortfolio(Guid SubmissionId)
{
    // Identifiers only (D-162).
    public override string ToString() => $"{nameof(EvaluatePortfolio)} {{ SubmissionId = {SubmissionId} }}";
}
