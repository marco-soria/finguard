namespace FinGuard.Compliance.Domain;

/// <summary>Stores evaluations. One per submission (D-193); nothing loads an evaluation back through it yet.</summary>
public interface IEvaluationRepository
{
    Task<bool> ExistsForSubmissionAsync(Guid submissionId, CancellationToken cancellationToken);

    /// <summary>Adds the evaluation to the unit of work; the caller's transaction saves it.</summary>
    void Add(Evaluation evaluation);
}
