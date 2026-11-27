using System.Globalization;
using FinGuard.BuildingBlocks.Results;

namespace FinGuard.Portfolio.Features.ListSubmissions;

/// <summary>The validated page request. All filters are optional and combine with AND (D-304).</summary>
internal sealed record SubmissionListFilter(
    int Limit, SubmissionCursor? Cursor, string? Status, DateOnly? CutOffFrom, DateOnly? CutOffTo, bool CurrentOnly)
{
    public const int DefaultLimit = 25;

    private static readonly string[] Statuses = ["Accepted", "Rejected"];

    /// <summary>Checks the rules in a fixed order; the first failure is the answer.</summary>
    public static Result<SubmissionListFilter> From(ListSubmissions query)
    {
        var limit = DefaultLimit;
        if (query.Limit is not null
            && (!int.TryParse(query.Limit, NumberStyles.None, CultureInfo.InvariantCulture, out limit)
                || limit < 1 || limit > ListSubmissionsHandler.Max))
        {
            return ListSubmissionsErrors.InvalidLimit;
        }

        SubmissionCursor? cursor = null;
        if (query.Cursor is not null)
        {
            if (!SubmissionCursor.TryDecode(query.Cursor, out var decoded))
            {
                return ListSubmissionsErrors.InvalidCursor;
            }

            cursor = decoded;
        }

        string? status = null;
        if (query.Status is not null)
        {
            status = Statuses.FirstOrDefault(s => s.Equals(query.Status, StringComparison.OrdinalIgnoreCase));
            if (status is null)
            {
                return ListSubmissionsErrors.InvalidStatus;
            }
        }

        if (!TryParseDate(query.CutOffFrom, out var from) || !TryParseDate(query.CutOffTo, out var to)
            || (from is not null && to is not null && from > to))
        {
            return ListSubmissionsErrors.InvalidCutOffRange;
        }

        var currentOnly = false;
        if (query.CurrentOnly is not null && !bool.TryParse(query.CurrentOnly, out currentOnly))
        {
            return ListSubmissionsErrors.InvalidCurrentOnly;
        }

        return new SubmissionListFilter(limit, cursor, status, from, to, currentOnly);
    }

    private static bool TryParseDate(string? value, out DateOnly? date)
    {
        date = null;
        if (value is null)
        {
            return true;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        date = parsed;
        return true;
    }
}
