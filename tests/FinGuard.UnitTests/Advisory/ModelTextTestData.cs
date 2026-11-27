namespace FinGuard.UnitTests.Advisory;

/// <summary>Model-style texts for the shares-heavy sample: a correct paraphrase and its variants.</summary>
internal static class ModelTextTestData
{
    /// <summary>Different wording from the template, every number copied from the facts, no disclaimer.</summary>
    public const string SharesHeavyParaphrase =
        "On 2026-06-30, the portfolio breaches its limit for Shares under policy version 1. Shares make up 73.33 % " +
        "of the portfolio while the limit is 25.00 %, so 293.33 % of the limit is used.\n\n" +
        "The proposal reduces Shares by $1,562,500.00 to $637,500.00, which is 85.00 % of its limit. The released " +
        "amount goes to Cash (+$693,374.43), Real estate (+$517,623.26) and Fixed income (+$351,502.31). After " +
        "these moves, every asset class is within its limit.";
}
