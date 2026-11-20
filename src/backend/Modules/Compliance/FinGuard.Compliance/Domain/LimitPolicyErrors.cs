using FinGuard.BuildingBlocks.Results;

namespace FinGuard.Compliance.Domain;

/// <summary>Why a policy configuration is invalid. Fixed messages, never the offending values.</summary>
public static class LimitPolicyErrors
{
    public static readonly Error LimitMissing = Error.Validation(
        "Compliance.Policy.LimitMissing", "The policy must define a limit for every asset class.");

    public static readonly Error LimitOutOfRange = Error.Validation(
        "Compliance.Policy.LimitOutOfRange", "Every limit must be greater than 0 and at most 1.");

    public static readonly Error AlertThresholdOutOfRange = Error.Validation(
        "Compliance.Policy.AlertThresholdOutOfRange", "The alert threshold must be greater than 0 and at most 1.");

    public static readonly Error TargetRatioOutOfRange = Error.Validation(
        "Compliance.Policy.TargetRatioOutOfRange", "The rebalance target ratio must be greater than 0 and less than 1.");

    public static readonly Error TargetNotBelowAlert = Error.Validation(
        "Compliance.Policy.TargetNotBelowAlert", "The rebalance target ratio must be less than the alert threshold.");

    public static readonly Error LimitsTooTightForTarget = Error.Validation(
        "Compliance.Policy.LimitsTooTightForTarget",
        "The rebalance target ratio times the sum of the limits must be at least 1.");
}
