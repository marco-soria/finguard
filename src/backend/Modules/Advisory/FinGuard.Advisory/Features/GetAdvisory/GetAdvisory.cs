namespace FinGuard.Advisory.Features.GetAdvisory;

/// <summary>Reads the advisory of one submission of the envelope's tenant (D-256).</summary>
public sealed record GetAdvisory(Guid SubmissionId)
{
    // Identifiers only (D-162).
    public override string ToString() => $"{nameof(GetAdvisory)} {{ SubmissionId = {SubmissionId} }}";
}
