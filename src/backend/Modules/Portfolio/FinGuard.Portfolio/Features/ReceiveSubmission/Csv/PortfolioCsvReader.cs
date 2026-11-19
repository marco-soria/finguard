using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using FinGuard.Portfolio.Persistence;

namespace FinGuard.Portfolio.Features.ReceiveSubmission.Csv;

internal sealed record ParsedPosition(int RowNumber, string PositionId, AssetClass AssetClass, decimal BookValue);

/// <summary>Positions when <see cref="ErrorCount"/> is 0; otherwise the first <see cref="PortfolioCsvReader.MaxErrors"/> errors.</summary>
internal sealed record PortfolioCsvResult(
    int RowCount, IReadOnlyList<ParsedPosition> Positions, IReadOnlyList<CsvError> Errors, int ErrorCount)
{
    public bool IsValid => ErrorCount == 0;
}

/// <summary>
/// Reads and validates a portfolio CSV (D-113, D-114, D-115). Pure: no I/O, no logging. CsvHelper only splits
/// fields (RFC 4180); every value rule is checked here.
/// </summary>
internal static partial class PortfolioCsvReader
{
    public const int MaxRows = 10_000;
    public const int MaxErrors = 100;
    public const int MaxPositionIdLength = 64;
    public const int MaxDescriptionLength = 150;

    /// <summary>numeric(18,2).</summary>
    public const decimal MaxBookValue = 9_999_999_999_999_999.99m;

    public const string PositionIdColumn = "position_id";
    public const string AssetClassColumn = "asset_class";
    public const string BookValueColumn = "book_value";
    public const string CurrencyColumn = "currency";
    public const string DescriptionColumn = "description";

    /// <summary>Canonical order: errors of one row follow it.</summary>
    private static readonly string[] Columns =
        [PositionIdColumn, AssetClassColumn, BookValueColumn, CurrencyColumn, DescriptionColumn];

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly CsvReaderOptions Options = new()
    {
        Separator = ',',
        HasHeader = true,
        Mode = CsvMode.Rfc4180,
        AllowLineBreaksInQuotes = true,
        SkipEmptyLines = true,
        DetectSeparatorHint = false,
        TrimWhitespace = false,
        TrackLineNumbers = true,
        MalformedData = CsvMalformedDataHandling.Throw,
        FieldCountMismatch = CsvFieldCountHandling.Throw,
        IncludeValuesInExceptions = false,
    };

    public static PortfolioCsvResult Read(ReadOnlyMemory<byte> content)
    {
        var errors = new ErrorList();
        if (!IsStrictUtf8(content.Span))
        {
            errors.Add(PortfolioCsvErrors.Create(PortfolioCsvErrors.InvalidEncoding));
            return errors.ToResult(0, []);
        }

        // The UTF-8 BOM is allowed (D-113); stripped here so it never reaches the first header name.
        if (content.Span.StartsWith(Encoding.UTF8.Preamble))
        {
            content = content[Encoding.UTF8.Preamble.Length..];
        }

        var positions = new List<ParsedPosition>();
        var rowCount = 0;
        // Spreadsheet row = input line minus the extra lines that earlier multi-line records took.
        var extraLines = 0;
        using var reader = CsvReader.Create(content, Options);
        try
        {
            if (ReadHeader(reader, errors) is not { } indexes)
            {
                return errors.ToResult(0, []);
            }

            extraLines = LineBreaks(reader.Headers);
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read())
            {
                var row = (int)reader.LineNumber - extraLines;
                extraLines += LineBreaks(reader.RawRecord);
                rowCount++;
                if (rowCount > MaxRows)
                {
                    errors.Add(PortfolioCsvErrors.Create(PortfolioCsvErrors.TooManyRows));
                    return errors.ToResult(rowCount, []);
                }

                if (ReadRow(reader, indexes, row, seenIds, errors) is { } position)
                {
                    positions.Add(position);
                }
            }
        }
        catch (CsvFormatException ex)
        {
            int? row = ex.LineNumber > 0 ? (int)ex.LineNumber - extraLines : null;
            errors.Add(PortfolioCsvErrors.Create(PortfolioCsvErrors.MalformedRow, row));
            return errors.ToResult(rowCount, []);
        }

        if (rowCount == 0)
        {
            errors.Add(PortfolioCsvErrors.Create(PortfolioCsvErrors.NoRows));
        }
        else if (errors.Count == 0 && positions.Sum(p => p.BookValue) <= 0)
        {
            errors.Add(PortfolioCsvErrors.Create(PortfolioCsvErrors.TotalNotPositive));
        }

        return errors.ToResult(rowCount, positions);
    }

    private static bool IsStrictUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            _ = StrictUtf8.GetCharCount(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>Field index per canonical column, or null after adding the header errors.</summary>
    private static int[]? ReadHeader(CsvReader reader, ErrorList errors)
    {
        var headers = reader.Headers;
        if (headers.Count == 0 || (headers.Count == 1 && string.IsNullOrWhiteSpace(headers[0])))
        {
            errors.Add(PortfolioCsvErrors.Create(PortfolioCsvErrors.HeaderMissing));
            return null;
        }

        var indexes = new int[Columns.Length];
        Array.Fill(indexes, -1);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < headers.Count; i++)
        {
            var name = headers[i].Trim().ToLowerInvariant();
            var canonical = Array.IndexOf(Columns, name);
            if (canonical < 0)
            {
                // The column name comes from the file: it is not echoed (D-115).
                errors.Add(PortfolioCsvErrors.Create(PortfolioCsvErrors.ColumnUnknown));
            }
            else if (indexes[canonical] >= 0)
            {
                if (reported.Add(name))
                {
                    errors.Add(PortfolioCsvErrors.Create(PortfolioCsvErrors.ColumnDuplicated, column: name));
                }
            }
            else
            {
                indexes[canonical] = i;
            }
        }

        for (var c = 0; c < Columns.Length; c++)
        {
            if (indexes[c] < 0)
            {
                errors.Add(PortfolioCsvErrors.Create(PortfolioCsvErrors.ColumnMissing, column: Columns[c]));
            }
        }

        return errors.Count == 0 ? indexes : null;
    }

    private static ParsedPosition? ReadRow(
        CsvReader reader, int[] indexes, int row, HashSet<string> seenIds, ErrorList errors)
    {
        var before = errors.Count;
        string Field(int column) => reader.GetString(indexes[column]).Trim();
        void Fail(string code, string column) => errors.Add(PortfolioCsvErrors.Create(code, row, column));

        var positionId = Field(0);
        if (positionId.Length == 0)
        {
            Fail(PortfolioCsvErrors.PositionIdRequired, PositionIdColumn);
        }
        else if (positionId.Length > MaxPositionIdLength)
        {
            Fail(PortfolioCsvErrors.PositionIdTooLong, PositionIdColumn);
        }
        else if (!seenIds.Add(positionId))
        {
            Fail(PortfolioCsvErrors.PositionIdDuplicated, PositionIdColumn);
        }

        var assetClassText = Field(1);
        var assetClass = default(AssetClass);
        if (assetClassText.Length == 0)
        {
            Fail(PortfolioCsvErrors.AssetClassRequired, AssetClassColumn);
        }
        else if (!AssetClassCodes.TryParse(assetClassText, out assetClass))
        {
            Fail(PortfolioCsvErrors.AssetClassUnknown, AssetClassColumn);
        }

        if (ValidateAmount(Field(2), out var bookValue) is { } amountError)
        {
            Fail(amountError, BookValueColumn);
        }

        var currency = Field(3);
        if (currency.Length == 0)
        {
            Fail(PortfolioCsvErrors.CurrencyRequired, CurrencyColumn);
        }
        else if (!currency.Equals("USD", StringComparison.OrdinalIgnoreCase))
        {
            Fail(PortfolioCsvErrors.CurrencyNotSupported, CurrencyColumn);
        }

        // Checked, never kept: free text stays in the raw file only (D-114).
        if (Field(4).Length > MaxDescriptionLength)
        {
            Fail(PortfolioCsvErrors.DescriptionTooLong, DescriptionColumn);
        }

        return errors.Count == before ? new ParsedPosition(row, positionId, assetClass, bookValue) : null;
    }

    /// <summary>Invariant digits, optional '.', at most 2 decimals; no sign, thousands separator or exponent (D-114).</summary>
    internal static string? ValidateAmount(string text, out decimal value)
    {
        value = 0m;
        if (text.Length == 0)
        {
            return PortfolioCsvErrors.BookValueRequired;
        }

        var match = AmountPattern().Match(text);
        if (!match.Success)
        {
            return PortfolioCsvErrors.BookValueInvalid;
        }

        if (match.Groups["sign"].Success)
        {
            return PortfolioCsvErrors.BookValueNegative;
        }

        if (match.Groups["decimals"].Length > 2)
        {
            return PortfolioCsvErrors.BookValueTooPrecise;
        }

        var number = match.Groups["number"].Value;
        return decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
               && value <= MaxBookValue
            ? null
            : PortfolioCsvErrors.BookValueOutOfRange;
    }

    private static int LineBreaks(IReadOnlyList<string> headers) => headers.Sum(h => h.Count(c => c == '\n'));

    private static int LineBreaks(ReadOnlySpan<byte> raw)
    {
        // CR LF, LF and a lone CR each count once.
        var count = 0;
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == (byte)'\n' || (raw[i] == (byte)'\r' && (i + 1 == raw.Length || raw[i + 1] != (byte)'\n')))
            {
                count++;
            }
        }

        return count;
    }

    [GeneratedRegex(@"^(?<sign>-)?(?<number>[0-9]+(?:\.(?<decimals>[0-9]+))?)$", RegexOptions.CultureInvariant)]
    private static partial Regex AmountPattern();

    private sealed class ErrorList
    {
        private readonly List<CsvError> _kept = [];

        public int Count { get; private set; }

        public void Add(CsvError error)
        {
            Count++;
            if (_kept.Count < MaxErrors)
            {
                _kept.Add(error);
            }
        }

        public PortfolioCsvResult ToResult(int rowCount, IReadOnlyList<ParsedPosition> positions) =>
            new(rowCount, Count == 0 ? positions : [], _kept, Count);
    }
}
