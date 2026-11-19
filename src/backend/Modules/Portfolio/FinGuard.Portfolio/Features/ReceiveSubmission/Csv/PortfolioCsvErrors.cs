namespace FinGuard.Portfolio.Features.ReceiveSubmission.Csv;

/// <summary>One validation finding. Row: header = 1, as in a spreadsheet; null for file errors (D-115).</summary>
public sealed record CsvError(int? Row, string? Column, string Code, string Message);

/// <summary>
/// Codes and their fixed English messages (D-115). Messages never contain cell values: they reach the API response
/// and the database, and a value may be personal or financial data.
/// </summary>
public static class PortfolioCsvErrors
{
    // File errors: reading stops.
    public const string InvalidEncoding = "Portfolio.Csv.InvalidEncoding";
    public const string HeaderMissing = "Portfolio.Csv.HeaderMissing";
    public const string ColumnMissing = "Portfolio.Csv.ColumnMissing";
    public const string ColumnUnknown = "Portfolio.Csv.ColumnUnknown";
    public const string ColumnDuplicated = "Portfolio.Csv.ColumnDuplicated";
    public const string MalformedRow = "Portfolio.Csv.MalformedRow";
    public const string NoRows = "Portfolio.Csv.NoRows";
    public const string TooManyRows = "Portfolio.Csv.TooManyRows";

    // Row errors: every row is checked.
    public const string PositionIdRequired = "Portfolio.Csv.PositionIdRequired";
    public const string PositionIdTooLong = "Portfolio.Csv.PositionIdTooLong";
    public const string PositionIdDuplicated = "Portfolio.Csv.PositionIdDuplicated";
    public const string PositionIdUnsafe = "Portfolio.Csv.PositionIdUnsafe";
    public const string AssetClassRequired = "Portfolio.Csv.AssetClassRequired";
    public const string AssetClassUnknown = "Portfolio.Csv.AssetClassUnknown";
    public const string BookValueRequired = "Portfolio.Csv.BookValueRequired";
    public const string BookValueInvalid = "Portfolio.Csv.BookValueInvalid";
    public const string BookValueNegative = "Portfolio.Csv.BookValueNegative";
    public const string BookValueTooPrecise = "Portfolio.Csv.BookValueTooPrecise";
    public const string BookValueOutOfRange = "Portfolio.Csv.BookValueOutOfRange";
    public const string CurrencyRequired = "Portfolio.Csv.CurrencyRequired";
    public const string CurrencyNotSupported = "Portfolio.Csv.CurrencyNotSupported";
    public const string DescriptionTooLong = "Portfolio.Csv.DescriptionTooLong";
    public const string DescriptionUnsafe = "Portfolio.Csv.DescriptionUnsafe";

    // File-level total, only when nothing else is wrong.
    public const string TotalNotPositive = "Portfolio.Csv.TotalNotPositive";

    public static IReadOnlyDictionary<string, string> Messages { get; } = new Dictionary<string, string>
    {
        [InvalidEncoding] = "The file is not valid UTF-8 text.",
        [HeaderMissing] = "The file has no header line.",
        [ColumnMissing] = "A required column is missing from the header.",
        [ColumnUnknown] = "The header has a column that is not expected.",
        [ColumnDuplicated] = "The header has the same column more than once.",
        [MalformedRow] = "The row has invalid quoting or a wrong number of fields.",
        [NoRows] = "The file has a header but no positions.",
        [TooManyRows] = $"The file has more than {PortfolioCsvReader.MaxRows} positions.",
        [PositionIdRequired] = "The position id is required.",
        [PositionIdTooLong] = $"The position id must be at most {PortfolioCsvReader.MaxPositionIdLength} characters.",
        [PositionIdDuplicated] = "The position id appears on an earlier row.",
        [PositionIdUnsafe] = "The position id must not start with =, +, -, @, a tab or a line break.",
        [AssetClassRequired] = "The asset class is required.",
        [AssetClassUnknown] = "The asset class must be cash, fixed_income, shares or real_estate.",
        [BookValueRequired] = "The book value is required.",
        [BookValueInvalid] = "The book value must be a plain number such as 1234.56.",
        [BookValueNegative] = "The book value must not be negative.",
        [BookValueTooPrecise] = "The book value must have at most 2 decimals.",
        [BookValueOutOfRange] = "The book value is too large.",
        [CurrencyRequired] = "The currency is required.",
        [CurrencyNotSupported] = "Only USD positions are supported in this version.",
        [DescriptionTooLong] = $"The description must be at most {PortfolioCsvReader.MaxDescriptionLength} characters.",
        [DescriptionUnsafe] = "The description must not start with =, +, -, @, a tab or a line break.",
        [TotalNotPositive] = "The total book value of the portfolio must be greater than zero.",
    };

    internal static CsvError Create(string code, int? row = null, string? column = null) =>
        new(row, column, code, Messages[code]);
}
