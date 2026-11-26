using System.Text.RegularExpressions;
using FinGuard.BuildingBlocks.Results;

namespace FinGuard.Advisory.Features.WriteAdvisory;

/// <summary>
/// Every number in a narrative must appear among the facts' display texts (D-254, ADR-0002). Pure. The template is
/// checked now; Brief #19 applies the same check to model output.
/// </summary>
public static partial class NarrativeValidator
{
    /// <summary>Fixed message: never the offending number (no amounts in errors or logs).</summary>
    public static readonly Error UnknownNumber = Error.Validation(
        "Advisory.Narrative.UnknownNumber", "The narrative contains a number that is not in the advisory facts.");

    public static Result Validate(string narrative, AdvisoryFacts facts)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        ArgumentNullException.ThrowIfNull(facts);

        var allowed = facts.Texts().SelectMany(Tokens).ToHashSet(StringComparer.Ordinal);
        return Tokens(narrative).All(allowed.Contains) ? Result.Success() : UnknownNumber;
    }

    private static IEnumerable<string> Tokens(string text) => NumericToken().Matches(text).Select(m => m.Value);

    // Digits with optional thousands separators and decimals: 1,562,500.00, 73.33, 2026, 1.
    [GeneratedRegex(@"\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumericToken();
}
