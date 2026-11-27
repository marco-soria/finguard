using System.Diagnostics.Metrics;

namespace FinGuard.Advisory.Features.Refinement;

/// <summary>Counts refinements by outcome (D-287). Tags carry the outcome only, never text or amounts.</summary>
public static class RefinementMetrics
{
    public const string MeterName = "FinGuard.Advisory";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> Refinements = Meter.CreateCounter<long>(
        "finguard.advisory.refinements", description: "Model narrative refinements by outcome.");

    public static void Record(RefinementOutcome outcome) =>
        Refinements.Add(1, new KeyValuePair<string, object?>("outcome", Tag(outcome)));

    public static string Tag(RefinementOutcome outcome) => outcome switch
    {
        RefinementOutcome.Accepted => "accepted",
        RefinementOutcome.Timeout => "timeout",
        RefinementOutcome.ModelError => "model_error",
        RefinementOutcome.UnknownNumber => "rejected_unknown_number",
        RefinementOutcome.StatusMismatch => "rejected_status_mismatch",
        RefinementOutcome.Format => "rejected_format",
        RefinementOutcome.Length => "rejected_length",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}
