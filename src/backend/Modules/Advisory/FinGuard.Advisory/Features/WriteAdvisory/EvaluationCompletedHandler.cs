using System.Text.Json;
using System.Text.Json.Serialization;
using FinGuard.Advisory.Features.Refinement;
using FinGuard.Advisory.Persistence;
using FinGuard.Compliance.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace FinGuard.Advisory.Features.WriteAdvisory;

/// <summary>
/// Writes the template advisory for a completed evaluation (D-251): event, facts, <see cref="TemplateNarrator"/>,
/// <see cref="NarrativeValidator"/>, store. Idempotent by <c>EventId</c> (ADR-0009). Writes through
/// <see cref="AdvisoryDbContext"/> only; Wolverine's EF middleware saves and commits. The envelope tenant is already
/// checked against the payload tenant by <c>TenantPropagationMiddleware</c>. With a model configured it marks the
/// advisory <c>Pending</c> and cascades <see cref="RefineNarrative"/> through the outbox, in the same
/// transaction, to add the AI context paragraph; the model call itself never runs here (D-281, D-293).
/// </summary>
public sealed partial class EvaluationCompletedHandler(
    TemplateNarrator narrator, IOptions<AdvisoryOptions> options, TimeProvider time,
    ILogger<EvaluationCompletedHandler> logger)
{
    internal static readonly JsonSerializerOptions FactsJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<OutgoingMessages> Handle(EvaluationCompleted @event, AdvisoryDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(db);

        var outgoing = new OutgoingMessages();
        if (await db.Set<Persistence.Advisory>().AnyAsync(a => a.SourceEventId == @event.EventId, ct))
        {
            return outgoing;
        }

        var facts = AdvisoryFactsBuilder.From(@event);
        var narrative = await narrator.NarrateAsync(facts, ct);
        // The template copies the facts' texts, so a failure here is a bug: throw and let it reach the error queue.
        if (NarrativeValidator.Validate(narrative, facts).IsFailure)
        {
            throw new InvalidOperationException(
                $"The template narrative for evaluation {@event.EvaluationId} failed validation.");
        }

        var advisory = Persistence.Advisory.Create(
            @event.EvaluationId, @event.SubmissionId, @event.EventId, JsonSerializer.Serialize(facts, FactsJson),
            narrative, TemplateNarrator.Name, time.GetUtcNow());
        if (options.Value.ModelEnabled)
        {
            advisory.RequestRefinement();
            outgoing.Add(new RefineNarrative(advisory.Id));
        }

        db.Add(advisory);
        LogWritten(logger, @event.EvaluationId);
        return outgoing;
    }

    // Identifiers only: never facts, narrative, amounts or dates (D-162).
    [LoggerMessage(Level = LogLevel.Information, Message = "Wrote advisory for evaluation {EvaluationId}")]
    private static partial void LogWritten(ILogger logger, Guid evaluationId);
}
