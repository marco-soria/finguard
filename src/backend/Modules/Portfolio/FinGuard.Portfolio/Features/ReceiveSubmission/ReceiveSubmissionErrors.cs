using FinGuard.BuildingBlocks.Results;

namespace FinGuard.Portfolio.Features.ReceiveSubmission;

internal static class ReceiveSubmissionErrors
{
    public const int MaxFileBytes = 5 * 1024 * 1024;

    public static readonly Error FileMissing =
        Error.Validation("Portfolio.Submission.FileMissing", "Attach the portfolio as a form field named 'file'.");

    public static readonly Error FileEmpty =
        Error.Validation("Portfolio.Submission.FileEmpty", "The uploaded file is empty.");

    public static readonly Error FileTooLarge =
        Error.Validation("Portfolio.Submission.FileTooLarge", "The uploaded file is larger than 5 MB.");

    public static readonly Error NotCsv =
        Error.Validation("Portfolio.Submission.NotCsv", "The uploaded file must have the .csv extension.");

    public static readonly Error CutOffDateInvalid =
        Error.Validation("Portfolio.Submission.CutOffDateInvalid", "The cut-off date is required in the format yyyy-MM-dd.");

    public static readonly Error CutOffDateInFuture =
        Error.Validation("Portfolio.Submission.CutOffDateInFuture", "The cut-off date must not be later than today (UTC).");
}
