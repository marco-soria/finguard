namespace FinGuard.Compliance.Features.GetEvaluation;

/// <summary>Reads the evaluation of one submission of the envelope's tenant (ADR-0009, D-197).</summary>
public sealed record GetEvaluation(Guid SubmissionId)
{
    // Identifiers only (D-162).
    public override string ToString() => $"{nameof(GetEvaluation)} {{ SubmissionId = {SubmissionId} }}";
}
