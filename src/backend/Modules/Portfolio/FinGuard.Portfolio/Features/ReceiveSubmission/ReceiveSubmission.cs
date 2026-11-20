namespace FinGuard.Portfolio.Features.ReceiveSubmission;

/// <summary>Stores and validates an uploaded portfolio file for the envelope's tenant (ADR-0009, D-111).</summary>
public sealed record ReceiveSubmission(DateOnly CutOffDate, string FileName, byte[] Content)
{
    // Identifiers and counts only: never the cut-off date, file name or bytes in logs (R-21, D-77, D-91, D-162).
    public override string ToString() => $"{nameof(ReceiveSubmission)} {{ SizeBytes = {Content.Length} }}";
}
