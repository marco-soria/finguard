using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinGuard.Advisory.Features.WriteAdvisory;
using FinGuard.Advisory.Infrastructure;
using static FinGuard.UnitTests.Advisory.AdvisoryTestData;

namespace FinGuard.UnitTests.Advisory;

/// <summary>D-284, D-293: the model sees class names and the target in words only: no figures, statuses or ids.</summary>
public sealed class NarratorPromptTests
{
    // Snapshot of the instructions. Changing them is a prompt change: bump NarratorPrompt.Version and this hash.
    private const string InstructionsSha256 = "FF1B85D3B66929FEB3E54D2AB30A87617B904C4FEBC9C45DFCE6FD4E2B29C365";
    private const string ExpectedVersion = "2026-11-27.2";

    [Fact]
    public void Input_SharesHeavy_NamesReducedAndReceivingClasses()
    {
        var root = JsonDocument.Parse(NarratorPrompt.Input(AdvisoryFactsBuilder.From(SharesHeavy()))).RootElement;

        Assert.Equal(["Cash", "Fixed income", "Shares", "Real estate"], Names(root, "assetClasses"));
        Assert.Equal(["Shares"], Names(root, "reducedClasses"));
        Assert.Equal(["Cash", "Fixed income", "Real estate"], Names(root, "receivingClasses"));
        Assert.Equal(NarratorPrompt.TargetInWords, root.GetProperty("rebalancingTarget").GetString());
    }

    [Fact]
    public void Input_Balanced_HasNoReducedOrReceivingClasses()
    {
        var root = JsonDocument.Parse(NarratorPrompt.Input(AdvisoryFactsBuilder.From(Balanced()))).RootElement;

        Assert.Equal((0, 0), (Names(root, "reducedClasses").Length, Names(root, "receivingClasses").Length));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Input_AnySample_HasNoDigitsStatusesOrIds(string sample)
    {
        var facts = AdvisoryFactsBuilder.From(Sample(sample));

        var json = NarratorPrompt.Input(facts);

        Assert.DoesNotContain(json, char.IsDigit);
        Assert.DoesNotContain("tenant", json, StringComparison.OrdinalIgnoreCase);
        foreach (var status in new[] { "Compliant", "Warning", "Breached" })
        {
            Assert.DoesNotContain(status, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static TheoryData<string> Samples => new() { "SharesHeavy", "Balanced", "WarningOnly" };

    [Fact]
    public void Instructions_Always_MatchTheVersionedSnapshot()
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NarratorPrompt.Instructions)));

        Assert.Equal((ExpectedVersion, InstructionsSha256), (NarratorPrompt.Version, hash));
    }

    [Fact]
    public void Instructions_Always_ContainNoDigits()
    {
        // A number in the instructions could be copied into the paragraph (no few-shot numbers, D-284).
        Assert.DoesNotContain(NarratorPrompt.Instructions, char.IsDigit);
    }

    private static FinGuard.Compliance.Contracts.Events.EvaluationCompleted Sample(string name) => name switch
    {
        "SharesHeavy" => SharesHeavy(),
        "Balanced" => Balanced(),
        _ => WarningOnly(),
    };

    private static string[] Names(JsonElement root, string property) =>
        root.GetProperty(property).EnumerateArray().Select(e => e.GetString()!).ToArray();
}
