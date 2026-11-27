using System.Text.Json;
using FinGuard.Advisory.Features.WriteAdvisory;
using FinGuard.Compliance.Contracts;

namespace FinGuard.Advisory.Infrastructure;

/// <summary>
/// The model's fixed instructions and its input (D-284, ADR-0015). The input is JSON of the facts' display texts,
/// class names and statuses only: no raw decimals, no ids, no tenant data and no free text (D-250). No few-shot
/// examples: a model copies their numbers. Change <see cref="Instructions"/> only with a new <see cref="Version"/>.
/// </summary>
public static class NarratorPrompt
{
    public const string Version = "2026-11-27.1";

    public const string Instructions = """
        You explain the result of a portfolio compliance evaluation to a finance reader, in English.
        The user message is JSON with the facts. Follow every rule:
        - Use only these facts. Add no other facts, opinions, forecasts or advice beyond the proposal they describe.
        - Copy every number exactly as it is written in the facts, with its $ sign, commas, decimals and " %" suffix.
          Never calculate, round, convert or abbreviate a number. Write no other number; write counts in words.
        - Write the text of "requiredPhrase" word for word. Never write any text listed in "forbiddenPhrases".
        - Explain the overall result, then each asset class whose status is Warning or Breached (its share of the
          portfolio, its limit and how much of the limit is used). When "proposalStatus" is not NotNeeded, explain how
          the proposal moves money: which classes are reduced or increased, by how much and to what amount.
        - Plain text only: no Markdown, no headings, no lists, no bold, no tables, no links, no HTML.
        - Write two to four short paragraphs separated by one blank line, under two hundred words in total.
        - Do not add a disclaimer or a closing remark; one is added after your text.
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Input(AdvisoryFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var input = new
        {
            cutOffDate = facts.CutOffDateText,
            policyVersion = facts.PolicyVersionText,
            overallStatus = facts.OverallStatus.ToString(),
            requiredPhrase = NarrativeValidator.StatusPhrase(facts.OverallStatus),
            forbiddenPhrases = Enum.GetValues<ComplianceStatusCode>()
                .Where(s => s != facts.OverallStatus)
                .Select(NarrativeValidator.StatusPhrase)
                .ToArray(),
            proposalStatus = facts.ProposalStatus.ToString(),
            rebalanceTargetOfLimit = facts.TargetRatioText,
            assetClasses = facts.Classes.Select(c => new
            {
                name = c.Name,
                status = c.Status.ToString(),
                bookValue = c.BookValueText,
                shareOfPortfolio = c.ShareText,
                limit = c.LimitText,
                limitUsed = c.UtilizationText,
                proposedBookValue = c.ProposedText,
                proposedChange = c.Change switch
                {
                    < 0m => "reduce by " + c.ChangeText,
                    > 0m => "increase by " + c.ChangeText,
                    _ => "unchanged",
                },
            }).ToArray(),
        };
        return JsonSerializer.Serialize(input, Json);
    }
}
