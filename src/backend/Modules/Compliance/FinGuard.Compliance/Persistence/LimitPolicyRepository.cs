using FinGuard.Compliance.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinGuard.Compliance.Persistence;

// Write side's read (ADR-0007): it feeds the evaluation command, so EF, not Dapper.
internal sealed class LimitPolicyRepository(ComplianceDbContext db) : ILimitPolicyRepository
{
    public async Task<LimitPolicy?> GetEffectiveAsync(DateOnly cutOffDate, CancellationToken cancellationToken)
    {
        var row = await db.Set<LimitPolicyRow>()
            .AsNoTracking()
            .Where(p => p.EffectiveFrom <= cutOffDate)
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        return row?.ToDomain();
    }
}
