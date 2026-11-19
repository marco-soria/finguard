namespace FinGuard.Portfolio.Features.GetSubmission;

/// <summary>Reads one submission of the envelope's tenant (ADR-0009, D-101).</summary>
public sealed record GetSubmission(Guid SubmissionId)
{
    // Identifiers only in logs (R-21, D-77).
    public override string ToString() => $"{nameof(GetSubmission)} {{ SubmissionId = {SubmissionId} }}";
}
