using FinGuard.Portfolio.Features.ListSubmissions;

namespace FinGuard.UnitTests.Portfolio;

public sealed class SubmissionListFilterTests
{
    [Fact]
    public void From_NoParameters_UsesDefaults()
    {
        var filter = SubmissionListFilter.From(new ListSubmissions()).Value;

        Assert.Equal(new SubmissionListFilter(25, null, null, null, null, false), filter);
    }

    [Fact]
    public void From_AllParametersValid_ParsesThem()
    {
        var cursor = new SubmissionCursor(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), Guid.NewGuid());

        var filter = SubmissionListFilter.From(
            new ListSubmissions("100", cursor.Encode(), "rejected", "2026-01-01", "2026-01-01", "TRUE")).Value;

        Assert.Equal(
            new SubmissionListFilter(100, cursor, "Rejected", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), true),
            filter);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("-1")]
    [InlineData("+5")]
    [InlineData(" 5")]
    [InlineData("2.5")]
    [InlineData("abc")]
    [InlineData("")]
    public void From_LimitOutOfRangeOrNotANumber_ReturnsInvalidLimit(string limit) =>
        AssertError(new ListSubmissions(Limit: limit), "Portfolio.Submissions.InvalidLimit");

    [Theory]
    [InlineData("1")]
    [InlineData("100")]
    public void From_LimitAtBoundary_IsAccepted(string limit) =>
        Assert.True(SubmissionListFilter.From(new ListSubmissions(Limit: limit)).IsSuccess);

    [Fact]
    public void From_MalformedCursor_ReturnsInvalidCursor() =>
        AssertError(new ListSubmissions(Cursor: "garbage"), "Portfolio.Submissions.InvalidCursor");

    [Theory]
    [InlineData("Pending")]
    [InlineData("0")]
    [InlineData("")]
    public void From_UnknownStatus_ReturnsInvalidStatus(string status) =>
        AssertError(new ListSubmissions(Status: status), "Portfolio.Submissions.InvalidStatus");

    [Theory]
    [InlineData("2026-02-01", "2026-01-31")]
    [InlineData("2026-13-01", null)]
    [InlineData(null, "01/02/2026")]
    [InlineData("2026-01-01T00:00:00", null)]
    public void From_BadCutOffRange_ReturnsInvalidCutOffRange(string? from, string? to) =>
        AssertError(new ListSubmissions(CutOffFrom: from, CutOffTo: to), "Portfolio.Submissions.InvalidCutOffRange");

    [Fact]
    public void From_OnlyCutOffFrom_IsAccepted() =>
        Assert.Equal(new DateOnly(2026, 1, 1),
            SubmissionListFilter.From(new ListSubmissions(CutOffFrom: "2026-01-01")).Value.CutOffFrom);

    [Theory]
    [InlineData("yes")]
    [InlineData("1")]
    public void From_CurrentOnlyNotBoolean_ReturnsInvalidCurrentOnly(string value) =>
        AssertError(new ListSubmissions(CurrentOnly: value), "Portfolio.Submissions.InvalidCurrentOnly");

    [Fact]
    public void From_SeveralInvalid_ReturnsTheFirstRuleInOrder() =>
        AssertError(new ListSubmissions(Limit: "0", Status: "x", CutOffFrom: "bad"), "Portfolio.Submissions.InvalidLimit");

    [Fact]
    public void Errors_Never_EchoTheInput()
    {
        const string input = "2099-99-99<script>";

        var error = SubmissionListFilter.From(new ListSubmissions(CutOffFrom: input)).Error!;

        Assert.DoesNotContain(input, error.Description, StringComparison.Ordinal);
    }

    private static void AssertError(ListSubmissions query, string code)
    {
        var result = SubmissionListFilter.From(query);

        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error.Code);
    }
}
