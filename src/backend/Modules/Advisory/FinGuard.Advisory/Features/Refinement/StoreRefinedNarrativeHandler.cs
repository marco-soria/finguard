using FinGuard.Advisory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinGuard.Advisory.Features.Refinement;

/// <summary>
/// Stores a refinement outcome with EF in a short transaction (ADR-0007, D-281). Applies only while the advisory is
/// <c>Pending</c>, so a second delivery changes nothing. The tenant filter scopes the lookup to the envelope's tenant.
/// </summary>
public static class StoreRefinedNarrativeHandler
{
    public static async Task Handle(StoreRefinedNarrative message, AdvisoryDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(db);

        var advisory = await db.Set<Persistence.Advisory>().SingleOrDefaultAsync(a => a.Id == message.AdvisoryId, ct);
        advisory?.ApplyRefinement(message.Outcome, message.Narrative, message.ModelId);
    }
}
