using FinGuard.Compliance.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinGuard.Compliance.Persistence;

// Write side (ADR-0007): EF through the module context, whose tenant filter applies. Public so Wolverine builds it in
// generated code with the handler's own DbContext instead of service location (D-94).
public sealed class EvaluationRepository(ComplianceDbContext db) : IEvaluationRepository
{
    public Task<bool> ExistsForSubmissionAsync(Guid submissionId, CancellationToken cancellationToken) =>
        db.Set<EvaluationRow>().AnyAsync(e => e.SubmissionId == submissionId, cancellationToken);

    public void Add(Evaluation evaluation) => db.Add(EvaluationRow.From(evaluation));
}
