using FinGuard.Compliance.Features.GetEvaluation;

namespace FinGuard.UnitTests.Compliance;

public sealed class ProposalOrderingTests
{
    [Fact]
    public void Build_NoStoredStatus_ReturnsNull()
    {
        Assert.Null(ProposalOrdering.Build(null, [(Item("Cash", 10m, 0.5m, "Compliant"), 10m)]));
    }

    [Fact]
    public void Build_NotNeeded_HasNoMoves()
    {
        var proposal = ProposalOrdering.Build("NotNeeded", [(Item("Cash", 10m, 0.5m, "Compliant"), 10m)]);

        Assert.Equal("NotNeeded", proposal!.Status);
        Assert.Empty(proposal.Moves);
    }

    [Fact]
    public void Build_MixedMoves_BreachedThenWarningByUtilizationThenReceiversByAmountThenClassOrder()
    {
        // Two Warning reducers with equal utilization keep class order; two receivers with equal amounts too.
        // A Compliant class that does not move is left out.
        (ClassEvaluationItem, decimal)[] classes =
        [
            (Item("Cash", 100m, 0.95m, "Warning"), 90m),
            (Item("FixedIncome", 100m, 0.95m, "Warning"), 95m),
            (Item("Shares", 100m, 1.20m, "Breached"), 80m),
            (Item("RealEstate", 100m, 0.50m, "Compliant"), 100m),
        ];
        (ClassEvaluationItem, decimal)[] receivers =
        [
            (Item("Cash", 10m, 0.10m, "Compliant"), 15m),
            (Item("FixedIncome", 10m, 0.10m, "Compliant"), 30m),
            (Item("Shares", 10m, 0.20m, "Compliant"), 15m),
            (Item("RealEstate", 100m, 1.01m, "Breached"), 70m),
        ];

        var reducersFirst = ProposalOrdering.Build("Full", classes)!.Moves;
        var receiversByAmount = ProposalOrdering.Build("Full", receivers)!.Moves;

        Assert.Equal(
            [
                new MoveItem("Shares", 100m, 80m, -20m),
                new MoveItem("Cash", 100m, 90m, -10m),
                new MoveItem("FixedIncome", 100m, 95m, -5m),
            ],
            reducersFirst);
        Assert.Equal(["RealEstate", "FixedIncome", "Cash", "Shares"], receiversByAmount.Select(m => m.AssetClass));
    }

    private static ClassEvaluationItem Item(string assetClass, decimal bookValue, decimal utilization, string status) =>
        new(assetClass, bookValue, 0m, 0.25m, utilization, status);
}
