using System.Text.RegularExpressions;
using FinGuard.BuildingBlocks.Results;

namespace FinGuard.Advisory.Features.Refinement;

/// <summary>
/// The rules for the model's context paragraph (D-293, ADR-0015). The template states every figure and status, so the
/// paragraph may contain none: no digits, no amount or percent symbols or words, no status wording; plain text, one
/// paragraph of 150 to 700 characters. Pure. Errors are fixed messages: never the offending text.
/// </summary>
public static partial class AiContextValidator
{
    public const int MinLength = 150;
    public const int MaxLength = 700;

    public static readonly Error Digits = Error.Validation(
        "Advisory.Context.Digits", "The AI context contains a digit.");

    public static readonly Error Symbols = Error.Validation(
        "Advisory.Context.Symbols", "The AI context contains an amount or percent symbol or word.");

    public static readonly Error StatusWording = Error.Validation(
        "Advisory.Context.StatusWording", "The AI context contains compliance status wording.");

    public static readonly Error Format = Error.Validation(
        "Advisory.Context.Format", "The AI context is not plain text.");

    public static readonly Error Length = Error.Validation(
        "Advisory.Context.Length", "The AI context is not one paragraph of the allowed length.");

    /// <summary>Checks in this order: digits, symbols, status wording, format, length. Expects <see cref="Normalize"/> output.</summary>
    public static Result Validate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Any(char.IsDigit))
        {
            return Digits;
        }

        if (AmountOrPercent().IsMatch(text))
        {
            return Symbols;
        }

        if (StatusWords().IsMatch(text))
        {
            return StatusWording;
        }

        if (text.AsSpan().IndexOfAny(MarkupCharacters) >= 0 || Url().IsMatch(text))
        {
            return Format;
        }

        return text.Length is < MinLength or > MaxLength || text.Contains("\n\n", StringComparison.Ordinal)
            ? Length
            : Result.Success();
    }

    /// <summary>
    /// Whitespace only: CRLF to LF, single line breaks inside a paragraph to spaces, paragraphs trimmed and separated by
    /// one blank line. Never changes a word.
    /// </summary>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var paragraphs = ParagraphBreak().Split(text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim())
            .Select(p => LineBreak().Replace(p.Trim(), " "))
            .Where(p => p.Length > 0);
        return string.Join("\n\n", paragraphs);
    }

    private static readonly System.Buffers.SearchValues<char> MarkupCharacters =
        System.Buffers.SearchValues.Create("<>`#*|");

    // %, any currency symbol (\p{Sc}: $, €, £…), and currency or percent words.
    [GeneratedRegex(@"[%\p{Sc}]|\b(?:percent(?:age)?s?|per\s+cent|dollars?|usd|cents?)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex AmountOrPercent();

    [GeneratedRegex(
        @"\b(?:breach(?:es|ed)?|exceed(?:s|ed)?|warnings?|compliant)\b|\b(?:above|near|within)\s+the\s+limit\b|\blimit\s+used\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex StatusWords();

    [GeneratedRegex(@"https?://|www\.|\]\(", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Url();

    [GeneratedRegex(@"\n[ \t]*\n\s*", RegexOptions.CultureInvariant)]
    private static partial Regex ParagraphBreak();

    [GeneratedRegex(@"[ \t]*\n[ \t]*", RegexOptions.CultureInvariant)]
    private static partial Regex LineBreak();
}
