using FinGuard.Compliance.Domain;
using FinGuard.Compliance.Domain.Rebalancing;
using static FinGuard.UnitTests.Compliance.Domain.PolicyTestData;

namespace FinGuard.UnitTests.Compliance.Domain;

public sealed class RebalanceEngineTests(ITestOutputHelper output)
{
    [Fact]
    public void Propose_CompliantPortfolioAtBoundary_NotNeededWithoutChanges()
    {
        // cash, fixed income and shares at 0.8999 (the last Compliant boundary row), real estate 0.58038.
        var evaluation = Evaluate(Values(359_960m, 269_970m, 224_975m, 145_095m));

        Assert.Equal(ComplianceStatus.Compliant, evaluation.OverallStatus);
        Assert.Equal(ProposalStatus.NotNeeded, evaluation.Proposal.Status);
        Assert.All(evaluation.Proposal.Moves, m => Assert.Equal(0m, m.Change));
    }

    [Fact]
    public void Propose_SharesHeavySample_ReducesSharesToTargetAndSplitsSurplusByHeadroom()
    {
        // valid-shares-heavy.csv, T = 3,000,000.00. Targets floor_cents(0.85 x L x T): cash 1,020,000.00,
        // fixed income 765,000.00, shares 637,500.00, real estate 637,500.00.
        // Shares (utilization 2.9333, Breached) is the reducer: S = 2,200,000 - 637,500 = 1,562,500.00.
        // Receivers and headroom: cash 720,000, fixed income 365,000, real estate 537,500; H = 1,622,500.
        // floor_cents(S x h / H): cash 693,374.42 (.4221...), fixed income 351,502.31 (.3112...),
        // real estate 517,623.26 (.2665...); sum 1,562,499.99, leftover 0.01 to cash (largest headroom).
        var evaluation = Evaluate(Values(300_000m, 400_000m, 2_200_000m, 100_000m));

        Assert.Equal(ProposalStatus.Full, evaluation.Proposal.Status);
        Assert.Equal(
            [
                Move(AssetClass.Cash, 300_000m, 993_374.43m),
                Move(AssetClass.FixedIncome, 400_000m, 751_502.31m),
                Move(AssetClass.Shares, 2_200_000m, 637_500.00m),
                Move(AssetClass.RealEstate, 100_000m, 617_623.26m),
            ],
            evaluation.Proposal.Moves);
        Assert.Equal(3_000_000m, evaluation.Proposal.Moves.Sum(m => m.Proposed.Amount));
    }

    [Fact]
    public void Propose_ClassExactlyAtAlertThreshold_IsReducedToTarget()
    {
        // shares 225,000 / 1,000,000 / 0.25 = 0.90 (Warning); target 212,500.
        var evaluation = Evaluate(Values(350_000m, 250_000m, 225_000m, 175_000m));

        Assert.Equal(212_500m, Proposed(evaluation, AssetClass.Shares));
        Assert.Equal(ProposalStatus.Full, evaluation.Proposal.Status);
    }

    [Fact]
    public void Propose_MiddleClassBetweenTargetAndAlert_IsNotMoved()
    {
        // cash 0.875: at or above its target (340,000), below the alert threshold. Shares 0.90 is the reducer;
        // S = 12,500 goes to fixed income (h 5,000) and real estate (h 37,500): 1,470.58 and 11,029.41 + 0.01.
        var evaluation = Evaluate(Values(350_000m, 250_000m, 225_000m, 175_000m));

        Assert.Equal(
            [
                Move(AssetClass.Cash, 350_000m, 350_000m),
                Move(AssetClass.FixedIncome, 250_000m, 251_470.58m),
                Move(AssetClass.Shares, 225_000m, 212_500m),
                Move(AssetClass.RealEstate, 175_000m, 186_029.42m),
            ],
            evaluation.Proposal.Moves);
    }

    [Fact]
    public void Propose_BreachedAndWarning_BothReducedToTarget()
    {
        // cash 0.95 (Warning), shares 1.04 (Breached). S = 40,000 + 47,500; receivers fixed income and real estate.
        var evaluation = Evaluate(Values(380_000m, 200_000m, 260_000m, 160_000m));

        Assert.Equal(340_000m, Proposed(evaluation, AssetClass.Cash));
        Assert.Equal(212_500m, Proposed(evaluation, AssetClass.Shares));
        Assert.Equal(ProposalStatus.Full, evaluation.Proposal.Status);
        Assert.Equal(1_000_000m, evaluation.Proposal.Moves.Sum(m => m.Proposed.Amount));
    }

    [Fact]
    public void Propose_FractionalTargetsAndZeroClass_LeftoverCentsGoToLargestHeadroom()
    {
        // T = 100.03. Targets: cash 34.01 (34.0102), fixed income 25.50 (25.50765), shares and real estate 21.25.
        // Shares is the reducer: S = 58.78. Headroom: cash 24.01, fixed income 15.50, real estate 21.25 (a zero
        // class); H = 60.76. Shares of S: 23.22, 14.99, 20.55 (sum 58.76); leftover 0.02 to cash.
        var evaluation = Evaluate(Values(10.00m, 10.00m, 80.03m, 0m));

        Assert.Equal(
            [
                Move(AssetClass.Cash, 10.00m, 33.24m),
                Move(AssetClass.FixedIncome, 10.00m, 24.99m),
                Move(AssetClass.Shares, 80.03m, 21.25m),
                Move(AssetClass.RealEstate, 0m, 20.55m),
            ],
            evaluation.Proposal.Moves);
        Assert.Equal(ProposalStatus.Full, evaluation.Proposal.Status);
    }

    [Fact]
    public void Propose_LeftoverWithTiedHeadroom_GoesToFirstClassInEnumOrder()
    {
        // cash 0.9500000625 (Warning) gives S = 40,000.01; fixed income 0.8667 is a middle class. Shares and real
        // estate both have headroom 32,500: each gets floor_cents(20,000.005) = 20,000.00; the leftover cent goes to
        // shares, which comes before real estate.
        var evaluation = Evaluate(Values(380_000.01m, 259_999.99m, 180_000m, 180_000m));

        Assert.Equal(
            [
                Move(AssetClass.Cash, 380_000.01m, 340_000m),
                Move(AssetClass.FixedIncome, 259_999.99m, 259_999.99m),
                Move(AssetClass.Shares, 180_000m, 200_000.01m),
                Move(AssetClass.RealEstate, 180_000m, 200_000m),
            ],
            evaluation.Proposal.Moves);
    }

    [Fact]
    public void Propose_OneCentAllCash_IsPartialAndPreservesTotal()
    {
        // T = 0.01: every floored target is 0.00, so cash goes to 0.00 and no class is below its target. The cent goes
        // to the non-reducer with the largest headroom (all 0.00; fixed income first in enum order), which then sits
        // at 0.01 / 0.01 / 0.30 = 3.33 and is Breached.
        var evaluation = Evaluate(Values(0.01m, 0m, 0m, 0m));

        Assert.Equal(ProposalStatus.Partial, evaluation.Proposal.Status);
        Assert.Equal(
            [
                Move(AssetClass.Cash, 0.01m, 0m),
                Move(AssetClass.FixedIncome, 0m, 0.01m),
                Move(AssetClass.Shares, 0m, 0m),
                Move(AssetClass.RealEstate, 0m, 0m),
            ],
            evaluation.Proposal.Moves);
        var after = Evaluate(Values(0m, 0.01m, 0m, 0m));
        Assert.Equal(
            ComplianceStatus.Breached, after.Classes.Single(c => c.Utilization.Class == AssetClass.FixedIncome).Status);
    }

    [Fact]
    public void Propose_SameInputTwiceOrReordered_SameProposal()
    {
        var evaluation = Evaluate(Values(380_000m, 200_000m, 260_000m, 160_000m));
        var total = evaluation.Total;

        var first = RebalanceEngine.Propose(evaluation.Classes, total, evaluation.Policy);
        var second = RebalanceEngine.Propose(evaluation.Classes, total, evaluation.Policy);
        var reordered = RebalanceEngine.Propose(evaluation.Classes.Reverse().ToList(), total, evaluation.Policy);

        Assert.Equal(first.Moves, second.Moves);
        Assert.Equal(first.Moves, reordered.Moves);
        Assert.Equal(first.Status, reordered.Status);
    }

    [Fact]
    public void Propose_MissingClass_Throws()
    {
        var evaluation = Evaluate(Values(380_000m, 200_000m, 260_000m, 160_000m));

        Assert.Throws<ArgumentException>(
            () => RebalanceEngine.Propose(evaluation.Classes.Skip(1).ToList(), evaluation.Total, evaluation.Policy));
    }

    [Fact]
    public void Propose_GeneratedPortfolios_HoldEveryInvariant()
    {
        const int cases = 10_000;
        var random = new Random(17);
        var classes = Enum.GetValues<AssetClass>();
        var limits = SeedLimits();
        var statusCounts = new Dictionary<ProposalStatus, int>();
        var middleCases = 0;
        var fallbackCases = 0;

        for (var i = 0; i < cases; i++)
        {
            var values = RandomPortfolio(random);
            var evaluation = Evaluate(values);
            var total = evaluation.Total.Amount;
            var proposal = evaluation.Proposal;
            var current = classes.ToDictionary(c => c, c => values[c].Amount);
            var proposed = proposal.Moves.ToDictionary(m => m.Class, m => m.Proposed.Amount);
            var status = evaluation.Classes.ToDictionary(c => c.Utilization.Class, c => c.Status);
            var target = classes.ToDictionary(c => c, c => Math.Floor(0.85m * limits[c].Value * total * 100m) / 100m);
            statusCounts[proposal.Status] = statusCounts.GetValueOrDefault(proposal.Status) + 1;

            Assert.Equal(classes, proposal.Moves.Select(m => m.Class));
            Assert.Equal(total, proposed.Values.Sum());
            if (evaluation.OverallStatus == ComplianceStatus.Compliant)
            {
                Assert.Equal(ProposalStatus.NotNeeded, proposal.Status);
                Assert.Equal(current, proposed);
                continue;
            }

            var reducers = classes.Where(c => status[c] != ComplianceStatus.Compliant).ToList();
            var nonReducers = classes.Except(reducers).ToList();
            var receivers = nonReducers.Where(c => current[c] < target[c]).ToList();
            if (receivers.Count == 0)
            {
                fallbackCases++;
                receivers = [nonReducers.OrderByDescending(c => target[c] - current[c]).ThenBy(c => c).First()];
            }

            foreach (var c in reducers)
            {
                Assert.Equal(target[c], proposed[c]);
            }

            foreach (var c in receivers)
            {
                Assert.True(proposed[c] >= current[c], $"Case {i}: receiver {c} decreased.");
                Assert.True(proposed[c] <= target[c] + 0.03m, $"Case {i}: receiver {c} above target + 0.03.");
            }

            middleCases += nonReducers.Except(receivers).Any() ? 1 : 0;
            foreach (var c in nonReducers.Except(receivers))
            {
                Assert.Equal(current[c], proposed[c]);
            }

            var after = Evaluate(classes.ToDictionary(c => c, c => new Money(proposed[c])));
            var expected = after.OverallStatus == ComplianceStatus.Compliant ? ProposalStatus.Full : ProposalStatus.Partial;
            Assert.Equal(expected, proposal.Status);
        }

        output.WriteLine(
            $"{cases} portfolios: {string.Join(", ", statusCounts.OrderBy(p => p.Key).Select(p => $"{p.Key} {p.Value}"))}; " +
            $"with a middle class {middleCases}; no-receiver fallback {fallbackCases}");
    }

    // A total from 1.00 to 10^12 USD (uniform number of digits), split over the classes with random weights. Half the
    // cases use free weights (zero in about one case out of five, so zero-value classes are common); the other half
    // use weights near each class's limit, which yields Compliant, Warning and middle classes.
    private static Dictionary<AssetClass, Money> RandomPortfolio(Random random)
    {
        var digits = random.Next(3, 15);
        var low = 1L;
        for (var d = 1; d < digits; d++)
        {
            low *= 10;
        }

        var totalCents = random.NextInt64(low, (low * 10) + 1);
        int[] nearLimits = [400, 300, 250, 250];
        var weights = random.Next(2) == 0
            ? Enumerable.Range(0, 4).Select(_ => random.Next(5) == 0 ? 0 : random.Next(1, 1001)).ToArray()
            : nearLimits.Select(w => w * random.Next(70, 101) / 100).ToArray();
        if (weights.Sum() == 0)
        {
            weights[random.Next(4)] = 1;
        }

        var cents = new long[4];
        var weightTotal = weights.Sum();
        for (var k = 0; k < 3; k++)
        {
            cents[k] = (long)((decimal)totalCents * weights[k] / weightTotal);
        }

        cents[3] = totalCents - cents[0] - cents[1] - cents[2];
        return Values(cents[0] / 100m, cents[1] / 100m, cents[2] / 100m, cents[3] / 100m);
    }

    private static ClassMove Move(AssetClass assetClass, decimal current, decimal proposed) =>
        new(assetClass, new Money(current), new Money(proposed));

    private static decimal Proposed(Evaluation evaluation, AssetClass assetClass) =>
        evaluation.Proposal.Moves.Single(m => m.Class == assetClass).Proposed.Amount;
}
