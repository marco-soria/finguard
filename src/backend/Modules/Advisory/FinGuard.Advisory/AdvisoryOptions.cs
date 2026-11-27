using System.ComponentModel.DataAnnotations;

namespace FinGuard.Advisory;

/// <summary>Narrator settings (ADR-0015), section <c>Advisory:Narrator</c>, validated on start.</summary>
public sealed class AdvisoryOptions
{
    public const string Section = "Advisory:Narrator";

    /// <summary>How long one model call may take before the template stays (<c>Timeout</c>).</summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 90;

    /// <summary>True when the <c>narrator</c> connection string exists; never read from configuration.</summary>
    public bool ModelEnabled { get; set; }
}
