namespace FinGuard.Compliance.Features.GetEvaluation;

/// <summary>
/// Builds the API's <see cref="ProposalItem"/> in presentation order (D-235), the only place that order is defined:
/// reduced classes first (Breached before Warning, then utilization descending), then the receiving classes (amount
/// received descending); ties by asset class order. Classes that do not change are left out.
/// </summary>
internal static class ProposalOrdering
{
    private static readonly string[] ClassOrder = ["Cash", "FixedIncome", "Shares", "RealEstate"];
    private static readonly string[] StatusPrecedence = ["Breached", "Warning", "Compliant"];

    public static ProposalItem? Build(
        string? proposalStatus, IEnumerable<(ClassEvaluationItem Class, decimal ProposedBookValue)> classes)
    {
        if (proposalStatus is null)
        {
            return null;
        }

        var moves = classes
            .Select(c => (c.Class, Move: new MoveItem(
                c.Class.AssetClass, c.Class.BookValue, c.ProposedBookValue, c.ProposedBookValue - c.Class.BookValue)))
            .Where(c => c.Move.Change != 0m)
            .OrderBy(c => c.Move.Change < 0m ? 0 : 1)
            .ThenBy(c => c.Move.Change < 0m ? Array.IndexOf(StatusPrecedence, c.Class.Status) : 0)
            .ThenByDescending(c => c.Move.Change < 0m ? c.Class.Utilization : c.Move.Change)
            .ThenBy(c => Array.IndexOf(ClassOrder, c.Class.AssetClass))
            .Select(c => c.Move)
            .ToList();
        return new ProposalItem(proposalStatus, moves);
    }
}
