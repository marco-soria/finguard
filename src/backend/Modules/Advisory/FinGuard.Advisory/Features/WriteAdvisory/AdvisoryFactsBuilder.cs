using System.Globalization;
using FinGuard.Compliance.Contracts;
using FinGuard.Compliance.Contracts.Events;

namespace FinGuard.Advisory.Features.WriteAdvisory;

/// <summary>Turns <see cref="EvaluationCompleted"/> into <see cref="AdvisoryFacts"/>, formatting every number once.</summary>
public static class AdvisoryFactsBuilder
{
    public static AdvisoryFacts From(EvaluationCompleted @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        // Compliance guarantees one entry per class and a total above 0 (ADR-0011); anything else is a bug.
        var classCodes = Enum.GetValues<AssetClassCode>();
        if (!@event.Classes.Select(c => c.AssetClass).Order().SequenceEqual(classCodes))
        {
            throw new InvalidOperationException($"EvaluationCompleted {@event.EventId} does not carry one entry per class.");
        }

        var total = @event.Classes.Sum(c => c.BookValue);
        var classes = @event.Classes
            .OrderBy(c => c.AssetClass)
            .Select(c =>
            {
                // Share as the domain computes it (value / total), for display only; never used for a status.
                var share = c.BookValue / total;
                var change = c.ProposedBookValue - c.BookValue;
                return new ClassFacts(
                    c.AssetClass, Name(c.AssetClass), c.Status, c.BookValue, share, c.Limit, c.Utilization,
                    c.ProposedBookValue, change,
                    AdvisoryFormat.Money(c.BookValue), AdvisoryFormat.Percent(share), AdvisoryFormat.Percent(c.Limit),
                    AdvisoryFormat.Ratio(c.Utilization, c.Status, @event.AlertThreshold),
                    AdvisoryFormat.Money(c.ProposedBookValue), AdvisoryFormat.Money(Math.Abs(change)));
            })
            .ToList();

        return new AdvisoryFacts(
            @event.EvaluationId, @event.SubmissionId, @event.CutOffDate,
            @event.CutOffDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), @event.OverallStatus,
            @event.PolicyVersion, @event.PolicyVersion.ToString(CultureInfo.InvariantCulture), @event.ProposalStatus,
            @event.AlertThreshold, @event.RebalanceTargetRatio, AdvisoryFormat.Percent(@event.RebalanceTargetRatio),
            classes);
    }

    /// <summary>Class names as the SPA shows them.</summary>
    public static string Name(AssetClassCode assetClass) => assetClass switch
    {
        AssetClassCode.Cash => "Cash",
        AssetClassCode.FixedIncome => "Fixed income",
        AssetClassCode.Shares => "Shares",
        AssetClassCode.RealEstate => "Real estate",
        _ => throw new ArgumentOutOfRangeException(nameof(assetClass)),
    };
}
