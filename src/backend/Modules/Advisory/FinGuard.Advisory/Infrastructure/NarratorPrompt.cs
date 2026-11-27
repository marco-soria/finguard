using System.Text.Json;
using FinGuard.Advisory.Features.WriteAdvisory;

namespace FinGuard.Advisory.Infrastructure;

/// <summary>
/// The model's fixed instructions and its input (D-284, D-293, ADR-0015). The model writes one context paragraph next
/// to the template, which already states every figure and status. The input is class names only: which classes the
/// proposal reduces and which receive money, and the rebalancing target in words. No amounts, no ratios, no statuses,
/// no ids, no tenant data and no free text (D-250). No few-shot examples. Change <see cref="Instructions"/> only with a
/// new <see cref="Version"/>.
/// </summary>
public static class NarratorPrompt
{
    public const string Version = "2026-11-27.2";

    public const string Instructions = """
        You add one paragraph of context to a portfolio compliance advisory, for a finance reader, in English.
        The advisory above your paragraph already states every figure and every status. Never repeat or estimate them.
        The user message is JSON: the asset classes, the classes the rebalancing proposal reduces, the classes that
        receive the money, and the rebalancing target in words. Follow every rule:
        - Explain the reasoning behind the proposal: why a concentration close to a maximum allocation matters, and
          what moving the reduced classes back toward the target achieves for the portfolio. When no class is reduced,
          explain why keeping each class below its maximum allocation matters.
        - You may name the asset classes from the JSON. Write no digits, no numbers, no amounts and no percentages,
          and no currency or percent words.
        - Never use these words: breach, breached, exceeds, exceeded, above the limit, warning, near the limit,
          compliant, within the limit, limit used.
        - Give no investment advice: no buy or sell recommendations, no timing, no forecasts, no products.
        - Plain text only: exactly one paragraph, no Markdown, no headings, no lists, no bold, no links, no HTML.
        - Write between thirty and one hundred words. Add no disclaimer and no closing remark.
        """;

    /// <summary>The target in words: the ratio itself is a figure, and figures belong to the template only.</summary>
    public const string TargetInWords = "a fixed safety margin below the maximum allocation of each class";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Input(AdvisoryFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var input = new
        {
            assetClasses = facts.Classes.Select(c => c.Name).ToArray(),
            reducedClasses = facts.Classes.Where(c => c.Change < 0m).Select(c => c.Name).ToArray(),
            receivingClasses = facts.Classes.Where(c => c.Change > 0m).Select(c => c.Name).ToArray(),
            rebalancingTarget = TargetInWords,
        };
        return JsonSerializer.Serialize(input, Json);
    }
}
