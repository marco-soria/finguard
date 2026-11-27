using System.Text.RegularExpressions;
using FinGuard.BuildingBlocks.Results;
using FinGuard.Compliance.Contracts;

namespace FinGuard.Advisory.Features.WriteAdvisory;

/// <summary>
/// Every number in a narrative must appear among the facts' display texts (D-254, ADR-0002). Pure. Model text must
/// also pass the status, format and length rules (D-283, ADR-0015). Errors are fixed messages: never the offending
/// number or text (no amounts in errors or logs).
/// </summary>
public static partial class NarrativeValidator
{
    public const int MinModelLength = 200;
    public const int MaxModelLength = 1500;
    public const int MaxModelParagraphs = 5;

    public static readonly Error UnknownNumber = Error.Validation(
        "Advisory.Narrative.UnknownNumber", "The narrative contains a number that is not in the advisory facts.");

    public static readonly Error StatusMismatch = Error.Validation(
        "Advisory.Narrative.StatusMismatch", "The narrative does not state the evaluation's overall status.");

    public static readonly Error Format = Error.Validation(
        "Advisory.Narrative.Format", "The narrative is not plain text.");

    public static readonly Error Length = Error.Validation(
        "Advisory.Narrative.Length", "The narrative is too short, too long or has too many paragraphs.");

    /// <summary>The template's phrase for each overall status; model text must use its own and none of the others.</summary>
    public static string StatusPhrase(ComplianceStatusCode status) => status switch
    {
        ComplianceStatusCode.Compliant => "within every limit",
        ComplianceStatusCode.Warning => "near the limit",
        ComplianceStatusCode.Breached => "breaches",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static Result Validate(string narrative, AdvisoryFacts facts)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        ArgumentNullException.ThrowIfNull(facts);

        var allowed = facts.Texts().SelectMany(Tokens).ToHashSet(StringComparer.Ordinal);
        return Tokens(narrative).All(allowed.Contains) ? Result.Success() : UnknownNumber;
    }

    /// <summary>
    /// The four rules for model text, before the disclaimer is appended: numbers, status phrase, plain text, length.
    /// Expects <see cref="NormalizeModelText"/> output.
    /// </summary>
    public static Result ValidateModelText(string text, AdvisoryFacts facts)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(facts);

        var paragraphs = text.Split("\n\n").Length;
        if (text.Length is < MinModelLength or > MaxModelLength || paragraphs > MaxModelParagraphs)
        {
            return Length;
        }

        if (text.AsSpan().IndexOfAny(MarkupCharacters) >= 0 || Url().IsMatch(text))
        {
            return Format;
        }

        var expected = StatusPhrase(facts.OverallStatus);
        var others = Enum.GetValues<ComplianceStatusCode>().Where(s => s != facts.OverallStatus).Select(StatusPhrase);
        if (!Contains(text, expected) || others.Any(phrase => Contains(text, phrase)))
        {
            return StatusMismatch;
        }

        return Validate(text, facts);
    }

    /// <summary>
    /// Whitespace only: CRLF to LF, single line breaks inside a paragraph to spaces, paragraphs trimmed and separated by
    /// one blank line. Never changes a word or a number.
    /// </summary>
    public static string NormalizeModelText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var paragraphs = ParagraphBreak().Split(text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim())
            .Select(p => LineBreak().Replace(p.Trim(), " "))
            .Where(p => p.Length > 0);
        return string.Join("\n\n", paragraphs);
    }

    private static readonly System.Buffers.SearchValues<char> MarkupCharacters = System.Buffers.SearchValues.Create("<>`#*|");

    private static bool Contains(string text, string phrase) => text.Contains(phrase, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> Tokens(string text) => NumericToken().Matches(text).Select(m => m.Value);

    // Digits with optional thousands separators and decimals: 1,562,500.00, 73.33, 2026, 1.
    [GeneratedRegex(@"\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumericToken();

    [GeneratedRegex(@"https?://|www\.|\]\(", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Url();

    [GeneratedRegex(@"\n[ \t]*\n\s*", RegexOptions.CultureInvariant)]
    private static partial Regex ParagraphBreak();

    [GeneratedRegex(@"[ \t]*\n[ \t]*", RegexOptions.CultureInvariant)]
    private static partial Regex LineBreak();
}
