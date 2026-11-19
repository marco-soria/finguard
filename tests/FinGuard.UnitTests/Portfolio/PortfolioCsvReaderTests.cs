using System.Globalization;
using System.Text;
using FinGuard.Portfolio.Features.ReceiveSubmission.Csv;
using FinGuard.Portfolio.Persistence;
using E = FinGuard.Portfolio.Features.ReceiveSubmission.Csv.PortfolioCsvErrors;

namespace FinGuard.UnitTests.Portfolio;

public sealed class PortfolioCsvReaderTests
{
    private const string Header = "position_id,asset_class,book_value,currency,description";

    // ---- Encoding

    [Fact]
    public void Read_ValidUtf8_ReturnsPositions()
    {
        var result = Read($"{Header}\nP-1,cash,100.50,USD,Café account");

        Assert.True(result.IsValid);
        var position = Assert.Single(result.Positions);
        Assert.Equal(new ParsedPosition(2, "P-1", AssetClass.Cash, 100.50m), position);
        Assert.Equal(1, result.RowCount);
    }

    [Fact]
    public void Read_Utf8WithBom_IsAccepted()
    {
        var bytes = Encoding.UTF8.Preamble.ToArray().Concat(Encoding.UTF8.GetBytes($"{Header}\nP-1,cash,1,USD,")).ToArray();

        var result = PortfolioCsvReader.Read(bytes);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Read_InvalidByteSequence_ReturnsInvalidEncoding()
    {
        var bytes = Encoding.UTF8.GetBytes($"{Header}\nP-1,cash,1,USD,").Concat(new byte[] { 0xC3, 0x28 }).ToArray();

        var result = PortfolioCsvReader.Read(bytes);

        AssertSingleError(result, E.InvalidEncoding, null, null);
        Assert.Equal(0, result.RowCount);
    }

    // ---- Header

    [Theory]
    [InlineData(Header)]
    [InlineData("description,currency,book_value,asset_class,position_id")]
    [InlineData("POSITION_ID,ASSET_CLASS,BOOK_VALUE,CURRENCY,DESCRIPTION")]
    [InlineData(" position_id , asset_class,book_value ,currency,  description")]
    public void Read_HeaderVariant_IsAccepted(string header)
    {
        var values = new Dictionary<string, string>
        {
            ["position_id"] = "P-1",
            ["asset_class"] = "shares",
            ["book_value"] = "10",
            ["currency"] = "USD",
            ["description"] = "x",
        };
        var row = string.Join(',', header.Split(',').Select(h => values[h.Trim().ToLowerInvariant()]));

        var result = Read($"{header}\n{row}");

        Assert.True(result.IsValid);
        Assert.Equal(AssetClass.Shares, Assert.Single(result.Positions).AssetClass);
    }

    [Theory]
    [InlineData("position_id,asset_class,book_value,currency", E.ColumnMissing, "description")]
    [InlineData(Header + ",notes", E.ColumnUnknown, null)]
    [InlineData(Header + ",currency", E.ColumnDuplicated, "currency")]
    public void Read_BadHeader_ReturnsColumnError(string header, string code, string? column)
    {
        var result = Read($"{header}\nP-1,cash,1,USD,x");

        AssertSingleError(result, code, null, column);
        Assert.Empty(result.Positions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n")]
    public void Read_NoHeaderLine_ReturnsHeaderMissing(string csv) =>
        AssertSingleError(Read(csv), E.HeaderMissing, null, null);

    [Fact]
    public void Read_OldMarketValueHeader_ReturnsColumnUnknownAndColumnMissing()
    {
        var result = Read("position_id,asset_class,market_value_usd,currency,description\nP-1,cash,1,USD,x");

        Assert.Equal(
            [(E.ColumnUnknown, (string?)null), (E.ColumnMissing, "book_value")],
            result.Errors.Select(e => (e.Code, e.Column)));
        Assert.Empty(result.Positions);
    }

    [Fact]
    public void Read_HeaderOnly_ReturnsNoRows() => AssertSingleError(Read($"{Header}\n"), E.NoRows, null, null);

    // ---- Rows: one case per row error code

    public static TheoryData<string, string, string> RowErrors => new()
    {
        { ",cash,1,USD,x", E.PositionIdRequired, "position_id" },
        { $"{new string('P', 65)},cash,1,USD,x", E.PositionIdTooLong, "position_id" },
        { "P-1,,1,USD,x", E.AssetClassRequired, "asset_class" },
        { "P-1,crypto,1,USD,x", E.AssetClassUnknown, "asset_class" },
        { "P-1,cash,,USD,x", E.BookValueRequired, "book_value" },
        { "P-1,cash,abc,USD,x", E.BookValueInvalid, "book_value" },
        { "P-1,cash,-1,USD,x", E.BookValueNegative, "book_value" },
        { "P-1,cash,1.505,USD,x", E.BookValueTooPrecise, "book_value" },
        { "P-1,cash,10000000000000000.00,USD,x", E.BookValueOutOfRange, "book_value" },
        { "P-1,cash,1,,x", E.CurrencyRequired, "currency" },
        { "P-1,cash,1,EUR,x", E.CurrencyNotSupported, "currency" },
        { $"P-1,cash,1,USD,{new string('d', 151)}", E.DescriptionTooLong, "description" },
    };

    [Theory]
    [MemberData(nameof(RowErrors))]
    public void Read_BadRow_ReturnsRowError(string row, string code, string column)
    {
        var result = Read($"{Header}\nOK-1,cash,5,USD,fine\n{row}");

        AssertSingleError(result, code, 3, column);
        Assert.Empty(result.Positions);
        Assert.Equal(2, result.RowCount);
    }

    [Fact]
    public void Read_DuplicatedPositionId_ReportsSecondOccurrence()
    {
        var result = Read($"{Header}\nP-1,cash,1,USD,\nP-2,cash,1,USD,\nP-1,shares,1,USD,");

        AssertSingleError(result, E.PositionIdDuplicated, 4, "position_id");
    }

    [Fact]
    public void Read_SeveralErrorsInOneRow_OrdersByCanonicalColumn()
    {
        // Columns reordered in the file; errors still follow the canonical header order.
        var result = Read("description,currency,book_value,asset_class,position_id\nx,EUR,-1,,");

        Assert.Equal(
            ["position_id", "asset_class", "book_value", "currency"],
            result.Errors.Select(e => e.Column));
        Assert.All(result.Errors, e => Assert.Equal(2, e.Row));
    }

    [Fact]
    public void Read_ErrorsOnSeveralRows_OrdersByRow()
    {
        var result = Read($"{Header}\nP-1,cash,1,EUR,\nP-2,,1,USD,");

        Assert.Equal([(2, "currency"), (3, "asset_class")], result.Errors.Select(e => (e.Row!.Value, e.Column!)));
    }

    [Fact]
    public void Read_FieldsWithSurroundingSpacesAndMixedCase_AreNormalised()
    {
        var result = Read($"{Header}\n  P-1 , Fixed_Income , 1.5 , usd , x ");

        Assert.Equal(new ParsedPosition(2, "P-1", AssetClass.FixedIncome, 1.5m), Assert.Single(result.Positions));
    }

    // ---- Row numbering (header = 1, as in a spreadsheet)

    [Fact]
    public void Read_QuotedMultiLineDescription_CountsAsOneRow()
    {
        var result = Read($"{Header}\nP-1,cash,1,USD,\"line one\nline two\nline three\"\nP-2,cash,1,EUR,");

        AssertSingleError(result, E.CurrencyNotSupported, 3, "currency");
    }

    [Fact]
    public void Read_CrLfLineEndings_NumbersRowsAsLf()
    {
        var result = Read($"{Header}\r\nP-1,cash,1,USD,\"a\r\nb\"\r\nP-2,cash,1,EUR,\r\n");

        AssertSingleError(result, E.CurrencyNotSupported, 3, "currency");
    }

    [Fact]
    public void Read_BlankLines_AreIgnoredButKeepTheirRowNumber()
    {
        var result = Read($"{Header}\n\nP-1,cash,1,USD,\n\n\nP-2,cash,1,EUR,\n\n");

        AssertSingleError(result, E.CurrencyNotSupported, 6, "currency");
        Assert.Equal(2, result.RowCount);
    }

    [Fact]
    public void Read_UnclosedQuote_ReturnsMalformedRow()
    {
        var result = Read($"{Header}\nP-1,cash,1,USD,ok\nP-2,cash,1,USD,\"never closed\nP-3,cash,1,USD,");

        Assert.Equal(E.MalformedRow, Assert.Single(result.Errors).Code);
        Assert.Empty(result.Positions);
    }

    [Theory]
    [InlineData("P-2,cash,1,USD")]
    [InlineData("P-2,cash,1,USD,x,extra")]
    public void Read_WrongFieldCount_ReturnsMalformedRowWithRow(string row)
    {
        var result = Read($"{Header}\nP-1,cash,1,USD,ok\n{row}\nP-3,cash,1,USD,");

        AssertSingleError(result, E.MalformedRow, 3, null);
    }

    [Fact]
    public void Read_MalformedRowAfterRowErrors_KeepsEarlierErrors()
    {
        var result = Read($"{Header}\nP-1,cash,1,EUR,\nP-2,cash\n");

        Assert.Equal([E.CurrencyNotSupported, E.MalformedRow], result.Errors.Select(e => e.Code));
        Assert.Equal(2, result.ErrorCount);
    }

    // ---- Amounts

    [Theory]
    [InlineData("0", "0")]
    [InlineData("0.00", "0")]
    [InlineData("0.01", "0.01")]
    [InlineData("1", "1")]
    [InlineData("1.5", "1.5")]
    [InlineData("1.50", "1.5")]
    [InlineData("9999999999999999.99", "9999999999999999.99")]
    public void ValidateAmount_Accepted(string text, string expected)
    {
        Assert.Null(PortfolioCsvReader.ValidateAmount(text, out var value));
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), value);
    }

    [Theory]
    [InlineData("-1", E.BookValueNegative)]
    [InlineData("-0.5", E.BookValueNegative)]
    [InlineData("-0.01", E.BookValueNegative)]
    [InlineData("1.505", E.BookValueTooPrecise)]
    [InlineData("1,000", E.BookValueInvalid)]
    [InlineData("1e3", E.BookValueInvalid)]
    [InlineData("+1", E.BookValueInvalid)]
    [InlineData("abc", E.BookValueInvalid)]
    [InlineData(".5", E.BookValueInvalid)]
    [InlineData("1.", E.BookValueInvalid)]
    [InlineData("$1", E.BookValueInvalid)]
    [InlineData("10000000000000000.00", E.BookValueOutOfRange)]
    [InlineData("99999999999999999999999999999999999", E.BookValueOutOfRange)]
    [InlineData("", E.BookValueRequired)]
    public void ValidateAmount_Rejected(string text, string code) =>
        Assert.Equal(code, PortfolioCsvReader.ValidateAmount(text, out _));

    [Fact]
    public void Read_QuotedThousandsSeparator_IsInvalid()
    {
        var result = Read($"{Header}\nP-1,cash,\"1,000.00\",USD,");

        AssertSingleError(result, E.BookValueInvalid, 2, "book_value");
    }

    [Fact]
    public void Read_LeadingMinus_IsReportedAsNegativeNotInvalid()
    {
        var result = Read($"{Header}\nP-1,cash,-1,USD,");

        AssertSingleError(result, E.BookValueNegative, 2, "book_value");
    }

    // ---- Currency

    [Theory]
    [InlineData("USD")]
    [InlineData("usd")]
    [InlineData(" Usd ")]
    public void Read_UsdInAnyCase_IsAccepted(string currency) =>
        Assert.True(Read($"{Header}\nP-1,cash,1,{currency},").IsValid);

    [Theory]
    [InlineData("EUR", E.CurrencyNotSupported)]
    [InlineData("PEN", E.CurrencyNotSupported)]
    [InlineData("", E.CurrencyRequired)]
    public void Read_OtherCurrency_ReturnsCurrencyError(string currency, string code) =>
        AssertSingleError(Read($"{Header}\nP-1,cash,1,{currency},"), code, 2, "currency");

    [Fact]
    public void Read_NonUsdCurrency_ExplainsOnlyUsdIsSupported()
    {
        var error = Assert.Single(Read($"{Header}\nP-1,cash,1,EUR,").Errors);

        Assert.Equal("Only USD positions are supported in this version.", error.Message);
    }

    // ---- Limits

    [Fact]
    public void Read_ExactlyMaxRows_IsAccepted()
    {
        var result = Read(Rows(PortfolioCsvReader.MaxRows, _ => "cash,1,USD,"));

        Assert.True(result.IsValid);
        Assert.Equal((10_000, 10_000), (result.RowCount, result.Positions.Count));
    }

    [Fact]
    public void Read_OneRowOverMax_ReturnsTooManyRows()
    {
        var result = Read(Rows(PortfolioCsvReader.MaxRows + 1, _ => "cash,1,USD,"));

        AssertSingleError(result, E.TooManyRows, null, null);
        Assert.Equal(10_001, result.RowCount);
    }

    [Fact]
    public void Read_150BadRows_Keeps100ErrorsAndCountsAll()
    {
        var result = Read(Rows(150, _ => "cash,1,EUR,"));

        Assert.Equal((100, 150), (result.Errors.Count, result.ErrorCount));
        Assert.Equal(101, result.Errors[^1].Row);
    }

    [Theory]
    [InlineData(150, true)]
    [InlineData(151, false)]
    public void Read_DescriptionLength_Boundary(int length, bool valid) =>
        Assert.Equal(valid, Read($"{Header}\nP-1,cash,1,USD,{new string('d', length)}").IsValid);

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Read_PositionIdLength_Boundary(int length, bool valid) =>
        Assert.Equal(valid, Read($"{Header}\n{new string('p', length)},cash,1,USD,").IsValid);

    // ---- Total

    [Fact]
    public void Read_AllValuesZero_ReturnsTotalNotPositive()
    {
        var result = Read($"{Header}\nP-1,cash,0,USD,\nP-2,shares,0.00,USD,");

        AssertSingleError(result, E.TotalNotPositive, null, null);
        Assert.Empty(result.Positions);
    }

    [Fact]
    public void Read_ZeroTotalWithOtherErrors_DoesNotReportTotal()
    {
        var result = Read($"{Header}\nP-1,cash,0,EUR,");

        AssertSingleError(result, E.CurrencyNotSupported, 2, "currency");
    }

    [Fact]
    public void Read_ZeroValueRowInPositiveTotal_IsAccepted() =>
        Assert.True(Read($"{Header}\nP-1,cash,0,USD,\nP-2,shares,10,USD,").IsValid);

    // ---- Messages never echo cell content (D-115)

    public static TheoryData<string, string> EchoCases => new()
    {
        { "MARKER-ID,cash,1,USD,x\nMARKER-ID,cash,1,USD,x", "MARKER-ID" },
        { $"{new string('M', 70)},cash,1,USD,x", new string('M', 70) },
        { "P-1,MARKERCLASS,1,USD,x", "MARKERCLASS" },
        { "P-1,cash,987654abc,USD,x", "987654abc" },
        { "P-1,cash,-987654,USD,x", "987654" },
        { "P-1,cash,987.654,USD,x", "987.654" },
        { "P-1,cash,98765432109876543210,USD,x", "98765432109876543210" },
        { "P-1,cash,1,MARKERCCY,x", "MARKERCCY" },
        { $"P-1,cash,1,USD,MARKER{new string('d', 500)}", "MARKER" },
    };

    [Theory]
    [MemberData(nameof(EchoCases))]
    public void Read_ErrorMessages_NeverContainTheCellValue(string rows, string value)
    {
        var result = Read($"{Header}\n{rows}");

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, e => Assert.DoesNotContain(value, e.Message, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Read_UnknownColumnMessage_DoesNotEchoTheColumnName()
    {
        var result = Read($"{Header},MARKERCOLUMN\nP-1,cash,1,USD,x,y");

        Assert.All(result.Errors, e => Assert.DoesNotContain("MARKERCOLUMN", e.Message + e.Column, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Messages_CoverEveryCodeWithAFixedSentence()
    {
        var codes = typeof(PortfolioCsvErrors).GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(22, codes.Count);
        Assert.All(codes, code =>
        {
            Assert.StartsWith("Portfolio.Csv.", code, StringComparison.Ordinal);
            Assert.EndsWith(".", PortfolioCsvErrors.Messages[code], StringComparison.Ordinal);
            Assert.True(PortfolioCsvErrors.Messages[code].Length <= 200);
        });
    }

    private static PortfolioCsvResult Read(string csv) => PortfolioCsvReader.Read(Encoding.UTF8.GetBytes(csv));

    private static string Rows(int count, Func<int, string> rest) =>
        Header + "\n" + string.Join('\n', Enumerable.Range(1, count).Select(i => $"P-{i},{rest(i)}"));

    private static void AssertSingleError(PortfolioCsvResult result, string code, int? row, string? column)
    {
        var error = Assert.Single(result.Errors);
        Assert.Equal((code, row, column), (error.Code, error.Row, error.Column));
        Assert.Equal(1, result.ErrorCount);
        Assert.False(result.IsValid);
    }
}
