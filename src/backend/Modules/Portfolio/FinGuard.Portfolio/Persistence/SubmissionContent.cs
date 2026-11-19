using FinGuard.BuildingBlocks.Tenancy;

namespace FinGuard.Portfolio.Persistence;

/// <summary>The raw uploaded bytes, 1:1 with <see cref="Submission"/>, kept apart so metadata queries never load them (D-87).</summary>
internal sealed class SubmissionContent : ITenantOwned
{
    private SubmissionContent()
    {
    }

    public Guid SubmissionId { get; private set; }

    public Guid TenantId { get; private set; }

    public byte[] Content { get; private set; } = [];

    public static SubmissionContent For(Guid submissionId, byte[] content) =>
        new() { SubmissionId = submissionId, Content = content };
}
