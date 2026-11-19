using FinGuard.BuildingBlocks.Tenancy;

namespace FinGuard.Portfolio.Persistence;

/// <summary>Metadata of an uploaded portfolio file. Immutable once stored (D-06); the bytes live in <see cref="SubmissionContent"/>.</summary>
internal sealed class Submission : ITenantOwned
{
    public const int FileNameMaxLength = 255;
    public const int Sha256Length = 64;
    public const int StatusMaxLength = 20;

    private Submission()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public DateOnly CutOffDate { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    public SubmissionStatus Status { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    /// <summary>Data rows read; 0 when the file failed before its rows.</summary>
    public int RowCount { get; private set; }

    /// <summary>All errors found; only the first 100 are stored (D-115).</summary>
    public int ErrorCount { get; private set; }

    // TenantId stays empty: TenantStampingInterceptor sets it from ITenantContext on save (ADR-0004).
    public static Submission Accept(
        Guid id, DateOnly cutOffDate, string fileName, int sizeBytes, string sha256, DateTimeOffset receivedAt,
        int rowCount) =>
        Create(id, cutOffDate, fileName, sizeBytes, sha256, receivedAt, SubmissionStatus.Accepted, rowCount, 0);

    public static Submission Reject(
        Guid id, DateOnly cutOffDate, string fileName, int sizeBytes, string sha256, DateTimeOffset receivedAt,
        int rowCount, int errorCount) =>
        Create(id, cutOffDate, fileName, sizeBytes, sha256, receivedAt, SubmissionStatus.Rejected, rowCount, errorCount);

    private static Submission Create(
        Guid id, DateOnly cutOffDate, string fileName, int sizeBytes, string sha256, DateTimeOffset receivedAt,
        SubmissionStatus status, int rowCount, int errorCount) =>
        new()
        {
            Id = id,
            CutOffDate = cutOffDate,
            FileName = fileName,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            Status = status,
            ReceivedAt = receivedAt,
            RowCount = rowCount,
            ErrorCount = errorCount,
        };
}

internal enum SubmissionStatus
{
    Accepted,
    Rejected,
}
