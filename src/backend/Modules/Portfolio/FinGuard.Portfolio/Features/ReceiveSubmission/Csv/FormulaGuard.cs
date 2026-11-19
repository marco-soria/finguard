namespace FinGuard.Portfolio.Features.ReceiveSubmission.Csv;

/// <summary>
/// Detects cells a spreadsheet would run as a formula (OWASP CSV Injection). Input rejection is defence in depth:
/// any future CSV or Excel export must still escape cells on output.
/// </summary>
internal static class FormulaGuard
{
    private const string Triggers = "=+-@＝＋－＠";

    /// <summary>
    /// True when the raw cell's leading whitespace contains tab, CR or LF, or when its first character after leading
    /// whitespace is <c>= + - @</c> or a full-width form of one of them.
    /// </summary>
    public static bool StartsWithFormulaTrigger(string raw)
    {
        // Same whitespace set as string.Trim(), which the reader applies before storing the value.
        var value = raw.TrimStart();
        if (raw.AsSpan(0, raw.Length - value.Length).IndexOfAny('\t', '\r', '\n') >= 0)
        {
            return true;
        }

        return value.Length > 0 && Triggers.Contains(value[0], StringComparison.Ordinal);
    }
}
