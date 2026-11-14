using System.Diagnostics.CodeAnalysis;

namespace FinGuard.BuildingBlocks.Results;

/// <summary>An expected failure. Codes look like <c>Portfolio.Submission.NotFound</c>.</summary>
[SuppressMessage("Naming", "CA1716", Justification = "Error is the agreed domain term (D-35); FinGuard is C# only.")]
public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error BusinessRule(string code, string description) => new(code, description, ErrorType.BusinessRule);
}
