using FinGuard.Compliance.Contracts.Events;
using FinGuard.Compliance.Features.EvaluatePortfolio;
using static FinGuard.UnitTests.Compliance.Domain.PolicyTestData;

namespace FinGuard.UnitTests.Advisory;

/// <summary>Events built the way Compliance builds them: real evaluation, real contract mapping.</summary>
internal static class AdvisoryTestData
{
    public static EvaluationCompleted Event(decimal cash, decimal fixedIncome, decimal shares, decimal realEstate) =>
        ContractMapping.ToEvent(Evaluate(Values(cash, fixedIncome, shares, realEstate)), Guid.NewGuid());

    /// <summary><c>samples/portfolios/valid-shares-heavy.csv</c>: Breached, proposal Full, three receivers.</summary>
    public static EvaluationCompleted SharesHeavy() => Event(300_000m, 400_000m, 2_200_000m, 100_000m);

    /// <summary><c>samples/portfolios/valid-balanced.csv</c>: Compliant, proposal NotNeeded.</summary>
    public static EvaluationCompleted Balanced() => Event(1_600_000m, 1_250_000m, 1_050_000m, 1_100_000m);

    /// <summary>Cash at 0.95 (Warning), shares at 0.88 (a middle class): proposal Full, two receivers.</summary>
    public static EvaluationCompleted WarningOnly() => Event(380_000m, 200_000m, 220_000m, 200_000m);

    public const string Disclaimer = "This text explains a deterministic calculation. It is not investment advice.";

    public const string SharesHeavyNarrative =
        "On 2026-06-30, the portfolio breaches the limit for Shares under policy version 1.\n\n" +
        "Shares holds 73.33 % of the portfolio against a limit of 25.00 %, so 293.33 % of the limit is used.\n\n" +
        "The proposal reduces Shares by $1,562,500.00 to $637,500.00, which is 85.00 % of its limit. " +
        "The amount moves to Cash (+$693,374.43), Real estate (+$517,623.26) and Fixed income (+$351,502.31). " +
        "Each of these stays at or below 85.00 % of its limit. " +
        "After these moves, every asset class is within its limit.\n\n" +
        Disclaimer;

    public const string BalancedNarrative =
        "On 2026-06-30, the portfolio is within every limit of policy version 1. No rebalancing is needed.\n\n" +
        Disclaimer;

    public const string WarningOnlyNarrative =
        "On 2026-06-30, the portfolio is within its limits under policy version 1, but Cash is near the limit.\n\n" +
        "Cash holds 38.00 % of the portfolio against a limit of 40.00 %, so 95.00 % of the limit is used.\n\n" +
        "The proposal reduces Cash by $40,000.00 to $340,000.00, which is 85.00 % of its limit. " +
        "The amount moves to Fixed income (+$32,592.60) and Real estate (+$7,407.40). " +
        "Each of these stays at or below 85.00 % of its limit. " +
        "After these moves, every asset class is within its limit.\n\n" +
        Disclaimer;
}
