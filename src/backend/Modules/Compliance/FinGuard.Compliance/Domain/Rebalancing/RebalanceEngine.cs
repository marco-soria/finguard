namespace FinGuard.Compliance.Domain.Rebalancing;

/// <summary>
/// Pure, deterministic rebalance proposal (ADR-0013, D-233, D-234). Reducers (Warning or Breached) go down to their
/// target <c>floor_cents(R x L x T)</c>; their surplus goes to the receivers (below their target) in proportion to
/// headroom; middle classes stay. A verification step re-evaluates the result with the same status rule.
/// </summary>
public static class RebalanceEngine
{
    public static RebalanceProposal Propose(IReadOnlyList<ClassResult> classes, Money total, PolicySnapshot policy)
    {
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(policy);

        var allClasses = Enum.GetValues<AssetClass>();
        var byClass = classes.ToDictionary(c => c.Utilization.Class);
        if (byClass.Count != allClasses.Length || classes.Count != allClasses.Length || total.Amount == 0m)
        {
            // No amounts in the message (D-162).
            throw new ArgumentException("Every asset class needs exactly one result and the total must be positive.", nameof(classes));
        }

        var current = allClasses.ToDictionary(c => c, c => byClass[c].Utilization.Value.Amount);
        if (allClasses.All(c => byClass[c].Status == ComplianceStatus.Compliant))
        {
            return new RebalanceProposal(ProposalStatus.NotNeeded, Moves(allClasses, current, current));
        }

        var target = allClasses.ToDictionary(
            c => c, c => FloorCents(policy.RebalanceTargetRatio.Value * policy.Limits[c].Value * total.Amount));
        var proposed = new Dictionary<AssetClass, decimal>(current);

        var reducers = allClasses.Where(c => byClass[c].Status != ComplianceStatus.Compliant).ToList();
        var surplus = 0m;
        foreach (var reducer in reducers)
        {
            surplus += current[reducer] - target[reducer];
            proposed[reducer] = target[reducer];
        }

        decimal Headroom(AssetClass c) => target[c] - current[c];
        var nonReducers = allClasses.Except(reducers).ToList();
        var receivers = nonReducers.Where(c => current[c] < target[c]).ToList();
        if (receivers.Count == 0)
        {
            // Floored targets can sum to less than the total. Some class is never a reducer: all four in alert would
            // need shares summing to at least AlertThreshold x sum(limits) > 1 (ADR-0013).
            receivers = [nonReducers.OrderByDescending(Headroom).ThenBy(c => c).First()];
        }

        var headroomTotal = receivers.Sum(Headroom);
        var distributable = headroomTotal > 0m ? Math.Min(surplus, headroomTotal) : 0m;
        var allocated = 0m;
        foreach (var receiver in receivers)
        {
            var share = distributable == 0m ? 0m : FloorCents(distributable * Headroom(receiver) / headroomTotal);
            proposed[receiver] += share;
            allocated += share;
        }

        // Leftover: rounding (<= 2 cents) or surplus above the headroom (<= 3 cents); largest headroom, enum ties (D-04).
        var largest = receivers.OrderByDescending(Headroom).ThenBy(c => c).First();
        proposed[largest] += surplus - allocated;

        var allCompliant = allClasses.All(c => ClassResult.For(
            ClassUtilization.Calculate(c, new Money(proposed[c]), total, policy.Limits[c]), policy.AlertThreshold)
            .Status == ComplianceStatus.Compliant);
        return new RebalanceProposal(
            allCompliant ? ProposalStatus.Full : ProposalStatus.Partial, Moves(allClasses, current, proposed));
    }

    private static decimal FloorCents(decimal amount) => Math.Floor(amount * 100m) / 100m;

    private static List<ClassMove> Moves(
        AssetClass[] classes, Dictionary<AssetClass, decimal> current, Dictionary<AssetClass, decimal> proposed) =>
        classes.Select(c => new ClassMove(c, new Money(current[c]), new Money(proposed[c]))).ToList();
}
