using FinGuard.Compliance.Contracts;

namespace FinGuard.Advisory.Features.WriteAdvisory;

/// <summary>
/// Deterministic English narrative from fixed sentences (ADR-0014). Every number is a display text copied from the
/// facts; counts and grammar use words, so the only digits in the output come from the facts (D-254).
/// </summary>
public sealed class TemplateNarrator : IAdvisoryNarrator
{
    /// <summary>The value stored in <c>advisory.advisories.narrator</c>.</summary>
    public const string Name = "Template";

    public const string Disclaimer = "This text explains a deterministic calculation. It is not investment advice.";

    public string? ModelId => null;

    public Task<string> NarrateAsync(AdvisoryFacts facts, CancellationToken ct) => Task.FromResult(Narrate(facts));

    public static string Narrate(AdvisoryFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        // Presentation order (D-235): Breached, then Warning by utilization descending; receivers by amount received
        // descending; ties by asset class order.
        var inAlert = facts.Classes
            .Where(c => c.Status != ComplianceStatusCode.Compliant)
            .OrderBy(c => c.Status == ComplianceStatusCode.Breached ? 0 : 1)
            .ThenByDescending(c => c.Utilization)
            .ThenBy(c => c.AssetClass)
            .ToList();
        var receivers = facts.Classes
            .Where(c => c.Change > 0m)
            .OrderByDescending(c => c.Change)
            .ThenBy(c => c.AssetClass)
            .ToList();

        var paragraphs = new List<string> { Headline(facts, inAlert) };
        if (inAlert.Count > 0)
        {
            paragraphs.Add(string.Join(" ", inAlert.Select(c =>
                $"{c.Name} holds {c.ShareText} of the portfolio against a limit of {c.LimitText}, so {c.UtilizationText} of the limit is used.")));
        }

        if (facts.ProposalStatus != ProposalStatusCode.NotNeeded)
        {
            paragraphs.Add(Proposal(facts, inAlert, receivers));
        }

        paragraphs.Add(Disclaimer);
        return string.Join("\n\n", paragraphs);
    }

    private static string Headline(AdvisoryFacts facts, List<ClassFacts> inAlert)
    {
        var date = facts.CutOffDateText;
        var version = facts.PolicyVersionText;
        switch (facts.OverallStatus)
        {
            case ComplianceStatusCode.Compliant:
                return $"On {date}, the portfolio is within every limit of policy version {version}. No rebalancing is needed.";
            case ComplianceStatusCode.Warning:
                return $"On {date}, the portfolio is within its limits under policy version {version}, but " +
                       $"{List(inAlert)} {(inAlert.Count == 1 ? "is" : "are")} near the limit.";
            case ComplianceStatusCode.Breached:
                var breached = inAlert.Where(c => c.Status == ComplianceStatusCode.Breached).ToList();
                return $"On {date}, the portfolio breaches the {(breached.Count == 1 ? "limit" : "limits")} for " +
                       $"{List(breached)} under policy version {version}.";
            default:
                throw new InvalidOperationException($"Unknown overall status for evaluation {facts.EvaluationId}.");
        }
    }

    private static string Proposal(AdvisoryFacts facts, List<ClassFacts> reducers, List<ClassFacts> receivers)
    {
        var target = facts.TargetRatioText;
        var sentences = new List<string>();
        if (reducers.Count > 0)
        {
            sentences.Add(
                $"The proposal reduces {List(reducers.Select(c => $"{c.Name} by {c.ChangeText} to {c.ProposedText}"))}" +
                (reducers.Count == 1 ? $", which is {target} of its limit." : $", each of which is {target} of its limit."));
        }

        if (receivers.Count > 0)
        {
            sentences.Add($"The amount moves to {List(receivers.Select(c => $"{c.Name} (+{c.ChangeText})"))}.");
            // Always true: a receiver may end a leftover cent above its target (R-49); the Full sentence carries the
            // guarantee, which Compliance's verification backs.
            sentences.Add(receivers.Count == 1
                ? $"The receiving class moves toward {target} of its limit."
                : $"Each receiving class moves toward {target} of its limit.");
        }

        sentences.Add(facts.ProposalStatus == ProposalStatusCode.Partial
            ? "Cent rounding leaves at least one asset class outside its target; review the amounts."
            : "After these moves, every asset class is within its limit.");
        return string.Join(" ", sentences);
    }

    private static string List(IEnumerable<ClassFacts> classes) => List(classes.Select(c => c.Name));

    private static string List(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count <= 1 ? string.Concat(list) : $"{string.Join(", ", list[..^1])} and {list[^1]}";
    }
}
