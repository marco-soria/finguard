using FinGuard.BuildingBlocks.Results;

namespace FinGuard.Portfolio.Features.ListSubmissions;

/// <summary>One code per rule; the first failing rule answers 400 (Brief #20). Fixed messages, never the input.</summary>
internal static class ListSubmissionsErrors
{
    public static readonly Error InvalidLimit = Error.Validation(
        "Portfolio.Submissions.InvalidLimit", "The limit must be a whole number from 1 to 100.");

    public static readonly Error InvalidCursor = Error.Validation(
        "Portfolio.Submissions.InvalidCursor", "The cursor is not valid. Start again from the first page.");

    public static readonly Error InvalidStatus = Error.Validation(
        "Portfolio.Submissions.InvalidStatus", "The status must be Accepted or Rejected.");

    public static readonly Error InvalidCutOffRange = Error.Validation(
        "Portfolio.Submissions.InvalidCutOffRange",
        "cutOffFrom and cutOffTo must be dates in the format yyyy-MM-dd, with cutOffFrom on or before cutOffTo.");

    public static readonly Error InvalidCurrentOnly = Error.Validation(
        "Portfolio.Submissions.InvalidCurrentOnly", "currentOnly must be true or false.");

    /// <summary>Every code in rule order, for the OpenAPI description.</summary>
    public static readonly IReadOnlyList<Error> All =
        [InvalidLimit, InvalidCursor, InvalidStatus, InvalidCutOffRange, InvalidCurrentOnly];
}
