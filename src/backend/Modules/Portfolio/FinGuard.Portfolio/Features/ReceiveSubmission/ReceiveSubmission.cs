namespace FinGuard.Portfolio.Features.ReceiveSubmission;

/// <summary>Stores an uploaded portfolio file for the envelope's tenant (ADR-0009). No parsing yet (Phase 2).</summary>
public sealed record ReceiveSubmission(DateOnly CutOffDate, string FileName, byte[] Content)
{
    // Never the file name or the bytes in logs (R-21, D-77, D-91).
    public override string ToString() =>
        $"{nameof(ReceiveSubmission)} {{ CutOffDate = {CutOffDate:yyyy-MM-dd}, SizeBytes = {Content.Length} }}";
}
