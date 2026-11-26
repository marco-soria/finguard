using FinGuard.Advisory.Features.WriteAdvisory;
using FinGuard.Compliance.Contracts;
using FinGuard.Compliance.Features.EvaluatePortfolio;
using static FinGuard.UnitTests.Compliance.Domain.PolicyTestData;

namespace FinGuard.UnitTests.Advisory;

public sealed class GeneratedAdvisoryTests(ITestOutputHelper output)
{
    [Fact]
    public void Narrate_GeneratedPortfolios_AlwaysPassTheValidator()
    {
        // The #17 generator with the same seed: evaluation, event, facts, template, validator.
        const int cases = 10_000;
        var random = new Random(17);
        var failures = 0;
        var statuses = new Dictionary<ProposalStatusCode, int>();
        var receiversAboveTarget = 0;
        var maxExcess = 0m;

        for (var i = 0; i < cases; i++)
        {
            var @event = ContractMapping.ToEvent(Evaluate(RandomPortfolio(random)), Guid.NewGuid());
            var facts = AdvisoryFactsBuilder.From(@event);
            var narrative = TemplateNarrator.Narrate(facts);

            failures += NarrativeValidator.Validate(narrative, facts).IsSuccess ? 0 : 1;
            statuses[facts.ProposalStatus] = statuses.GetValueOrDefault(facts.ProposalStatus) + 1;
            // Measures the "stays at or below the target" sentence (test-side arithmetic only).
            var total = facts.Classes.Sum(c => c.BookValue);
            var excess = facts.Classes
                .Where(c => c.Change > 0m)
                .Select(c => c.ProposedBookValue - (facts.RebalanceTargetRatio * c.Limit * total))
                .DefaultIfEmpty(0m)
                .Max();
            receiversAboveTarget += excess > 0m ? 1 : 0;
            maxExcess = Math.Max(maxExcess, excess);
        }

        output.WriteLine(
            $"{cases} portfolios: {string.Join(", ", statuses.OrderBy(p => p.Key).Select(p => $"{p.Key} {p.Value}"))}; " +
            $"validator failures {failures}; with a receiver above its target {receiversAboveTarget} " +
            $"(largest excess {maxExcess:0.0000} USD)");
        Assert.Equal(0, failures);
    }
}
