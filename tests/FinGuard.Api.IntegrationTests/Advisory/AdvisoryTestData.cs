namespace FinGuard.Api.IntegrationTests.Advisory;

internal static class AdvisoryTestData
{
    // Same golden text as FinGuard.UnitTests' TemplateNarratorTests for samples/portfolios/valid-shares-heavy.csv.
    public const string SharesHeavyNarrative =
        "On 2026-06-30, the portfolio breaches the limit for Shares under policy version 1.\n\n" +
        "Shares holds 73.33 % of the portfolio against a limit of 25.00 %, so 293.33 % of the limit is used.\n\n" +
        "The proposal reduces Shares by $1,562,500.00 to $637,500.00, which is 85.00 % of its limit. " +
        "The amount moves to Cash (+$693,374.43), Real estate (+$517,623.26) and Fixed income (+$351,502.31). " +
        "Each of these stays at or below 85.00 % of its limit. " +
        "After these moves, every asset class is within its limit.\n\n" +
        "This text explains a deterministic calculation. It is not investment advice.";

    public static byte[] Sample(string file) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", file));
}
