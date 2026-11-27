using System.Text.Json;
using Dapper;
using FinGuard.Advisory.Features.WriteAdvisory;
using FinGuard.BuildingBlocks.Tenancy;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FinGuard.Advisory.Features.Refinement;

/// <summary>
/// Asks the model for a narrative (D-281, ADR-0015). No DbContext, so Wolverine opens no transaction: the model call
/// may take seconds without holding a connection. Reads the facts with Dapper (own schema, <c>tenant_id</c> filter),
/// calls the narrator, normalizes and validates its text, and cascades <see cref="StoreRefinedNarrative"/>. Expected
/// model failures become a rejection reason (D-282): no retry, no error queue. Anything else follows the error policy.
/// </summary>
public sealed partial class RefineNarrativeHandler(IAdvisoryNarrator narrator, ILogger<RefineNarrativeHandler> logger)
{
    // Pending only: a redelivered message after the outcome was stored asks nothing.
    private const string Sql = """
        SELECT facts::text
        FROM advisory.advisories
        WHERE id = @AdvisoryId AND tenant_id = @TenantId AND refinement_status = 'Pending';
        """;

    private const int MaxModelIdLength = 64;

    public async Task<StoreRefinedNarrative?> Handle(
        RefineNarrative message, NpgsqlDataSource dataSource, ITenantContext tenant, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(tenant);

        string? json;
        await using (var connection = await dataSource.OpenConnectionAsync(ct))
        {
            json = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
                Sql, new { message.AdvisoryId, TenantId = tenant.RequiredTenantId }, cancellationToken: ct));
        }

        if (json is null)
        {
            return null;
        }

        var facts = JsonSerializer.Deserialize<AdvisoryFacts>(json, EvaluationCompletedHandler.FactsJson)
            ?? throw new InvalidOperationException($"Advisory {message.AdvisoryId} has no facts.");
        var result = await RefineAsync(message.AdvisoryId, facts, narrator, ct);

        RefinementMetrics.Record(result.Outcome);
        LogRefined(logger, message.AdvisoryId, result.Outcome);
        return result;
    }

    /// <summary>The model call and the checks, without I/O of its own: the unit-tested core.</summary>
    internal static async Task<StoreRefinedNarrative> RefineAsync(
        Guid advisoryId, AdvisoryFacts facts, IAdvisoryNarrator narrator, CancellationToken ct)
    {
        string text;
        try
        {
            text = await narrator.NarrateAsync(facts, ct);
        }
        catch (TimeoutException)
        {
            return Rejected(advisoryId, RefinementOutcome.Timeout);
        }
#pragma warning disable CA1031 // Any failure of the model call keeps the template (D-282); shutdown still cancels.
        catch (Exception) when (!ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            return Rejected(advisoryId, RefinementOutcome.ModelError);
        }

        var normalized = NarrativeValidator.NormalizeModelText(text);
        var validation = NarrativeValidator.ValidateModelText(normalized, facts);
        if (validation.IsFailure)
        {
            return Rejected(advisoryId, ReasonOf(validation.Error));
        }

        var modelId = narrator.ModelId is { Length: > 0 } id ? id[..Math.Min(id.Length, MaxModelIdLength)] : "unknown";
        // The disclaimer is code's, never the model's (D-283).
        return new StoreRefinedNarrative(
            advisoryId, RefinementOutcome.Accepted, normalized + "\n\n" + TemplateNarrator.Disclaimer, modelId);
    }

    private static StoreRefinedNarrative Rejected(Guid advisoryId, RefinementOutcome outcome) =>
        new(advisoryId, outcome, null, null);

    private static RefinementOutcome ReasonOf(BuildingBlocks.Results.Error error) =>
        error == NarrativeValidator.UnknownNumber ? RefinementOutcome.UnknownNumber
        : error == NarrativeValidator.StatusMismatch ? RefinementOutcome.StatusMismatch
        : error == NarrativeValidator.Format ? RefinementOutcome.Format
        : error == NarrativeValidator.Length ? RefinementOutcome.Length
        : throw new InvalidOperationException($"Unknown narrative error {error.Code}.");

    // Identifier and outcome code only: never prompts, completions, facts or amounts (D-162, D-287).
    [LoggerMessage(Level = LogLevel.Information, Message = "Refined advisory {AdvisoryId}: {Outcome}")]
    private static partial void LogRefined(ILogger logger, Guid advisoryId, RefinementOutcome outcome);
}
