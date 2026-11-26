using System.Text.Json;
using System.Text.Json.Serialization;
using FinGuard.Advisory.Persistence;
using FinGuard.Compliance.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinGuard.Advisory.Features.WriteAdvisory;

/// <summary>
/// Writes the template advisory for a completed evaluation (D-251): event, facts, <see cref="TemplateNarrator"/>,
/// <see cref="NarrativeValidator"/>, store. Idempotent by <c>EventId</c> (ADR-0009). Writes through
/// <see cref="AdvisoryDbContext"/> only; Wolverine's EF middleware saves and commits. The envelope tenant is already
/// checked against the payload tenant by <c>TenantPropagationMiddleware</c>.
/// </summary>
public sealed partial class EvaluationCompletedHandler(
    TemplateNarrator narrator, TimeProvider time, ILogger<EvaluationCompletedHandler> logger)
{
    internal static readonly JsonSerializerOptions FactsJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task Handle(EvaluationCompleted @event, AdvisoryDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(db);

        if (await db.Set<Persistence.Advisory>().AnyAsync(a => a.SourceEventId == @event.EventId, ct))
        {
            return;
        }

        var facts = AdvisoryFactsBuilder.From(@event);
        var narrative = await narrator.NarrateAsync(facts, ct);
        // The template copies the facts' texts, so a failure here is a bug: throw and let it reach the error queue.
        if (NarrativeValidator.Validate(narrative, facts).IsFailure)
        {
            throw new InvalidOperationException(
                $"The template narrative for evaluation {@event.EvaluationId} failed validation.");
        }

        db.Add(Persistence.Advisory.Create(
            @event.EvaluationId, @event.SubmissionId, @event.EventId, JsonSerializer.Serialize(facts, FactsJson),
            narrative, TemplateNarrator.Name, time.GetUtcNow()));
        LogWritten(logger, @event.EvaluationId);
    }

    // Identifiers only: never facts, narrative, amounts or dates (D-162).
    [LoggerMessage(Level = LogLevel.Information, Message = "Wrote advisory for evaluation {EvaluationId}")]
    private static partial void LogWritten(ILogger logger, Guid evaluationId);
}
