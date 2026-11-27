using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinGuard.Advisory.Features.WriteAdvisory;
using FinGuard.Advisory.Infrastructure;
using static FinGuard.UnitTests.Advisory.AdvisoryTestData;

namespace FinGuard.UnitTests.Advisory;

/// <summary>D-284: the model sees display texts, class names, statuses, the date and the policy version only.</summary>
public sealed class NarratorPromptTests
{
    // Snapshot of the instructions. Changing them is a prompt change: bump NarratorPrompt.Version and this hash.
    private const string InstructionsSha256 = "A86BE868B2CF532D8EFF966E45BAD4E30A8CEB8537FC5EDB7E2B76C57433C8DD";
    private const string ExpectedVersion = "2026-11-27.1";

    [Fact]
    public void Input_SharesHeavy_ContainsOnlyDisplayTextsAndNames()
    {
        var facts = AdvisoryFactsBuilder.From(SharesHeavy());
        var allowed = facts.Texts()
            .Concat(facts.Classes.SelectMany(c => new[] { "reduce by " + c.ChangeText, "increase by " + c.ChangeText }))
            .ToHashSet(StringComparer.Ordinal);

        var values = StringValues(JsonDocument.Parse(NarratorPrompt.Input(facts)).RootElement).ToList();

        Assert.All(values.Where(v => v.Any(char.IsDigit)), v => Assert.Contains(v, allowed));
    }

    [Fact]
    public void Input_SharesHeavy_HasNoRawDecimalsIdsOrNumbers()
    {
        var facts = AdvisoryFactsBuilder.From(SharesHeavy());

        var json = NarratorPrompt.Input(facts);

        Assert.DoesNotContain(facts.EvaluationId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(facts.SubmissionId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenant", json, StringComparison.OrdinalIgnoreCase);
        var shares = facts.Classes.Single(c => c.Name == "Shares");
        Assert.DoesNotContain(shares.Share.ToString(CultureInfo.InvariantCulture), json, StringComparison.Ordinal);
        Assert.DoesNotContain(shares.Utilization.ToString(CultureInfo.InvariantCulture), json, StringComparison.Ordinal);
        Assert.DoesNotContain(Kinds(JsonDocument.Parse(json).RootElement), k => k == JsonValueKind.Number);
    }

    [Fact]
    public void Input_Breached_RequiresBreachesAndForbidsTheOtherPhrases()
    {
        var root = JsonDocument.Parse(NarratorPrompt.Input(AdvisoryFactsBuilder.From(SharesHeavy()))).RootElement;

        Assert.Equal("breaches", root.GetProperty("requiredPhrase").GetString());
        Assert.Equal(
            ["within every limit", "near the limit"],
            root.GetProperty("forbiddenPhrases").EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    [Fact]
    public void Instructions_Always_MatchTheVersionedSnapshot()
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NarratorPrompt.Instructions)));

        Assert.Equal((ExpectedVersion, InstructionsSha256), (NarratorPrompt.Version, hash));
    }

    [Fact]
    public void Instructions_Always_ContainNoDigits()
    {
        // A number in the instructions could be copied into the narrative (no few-shot numbers, D-284).
        Assert.DoesNotContain(NarratorPrompt.Instructions, char.IsDigit);
    }

    private static IEnumerable<string> StringValues(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => [element.GetString()!],
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => StringValues(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(StringValues),
        _ => [],
    };

    private static IEnumerable<JsonValueKind> Kinds(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Kinds(p.Value)).Append(element.ValueKind),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Kinds).Append(element.ValueKind),
        _ => [element.ValueKind],
    };
}
