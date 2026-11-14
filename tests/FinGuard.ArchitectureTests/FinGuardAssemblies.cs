using System.Reflection;
using ArchUnitNET.Loader;
using Architecture = ArchUnitNET.Domain.Architecture;

namespace FinGuard.ArchitectureTests;

/// <summary>
/// Production assemblies discovered by convention from the test output folder, so a new module is covered
/// without editing the tests: a module is <c>FinGuard.&lt;Module&gt;</c> with a sibling <c>FinGuard.&lt;Module&gt;.Contracts</c>.
/// </summary>
internal static class FinGuardAssemblies
{
    public const string Prefix = "FinGuard.";
    public const string BuildingBlocksName = "FinGuard.BuildingBlocks";
    private const string ContractsSuffix = ".Contracts";

    public static IReadOnlyList<Assembly> All { get; } = Directory
        .GetFiles(AppContext.BaseDirectory, $"{Prefix}*.dll")
        .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))
        .Where(a => !a.GetName().Name!.EndsWith("Tests", StringComparison.Ordinal))
        .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
        .ToList();

    public static IReadOnlyList<Assembly> Contracts { get; } = All
        .Where(a => a.GetName().Name!.EndsWith(ContractsSuffix, StringComparison.Ordinal))
        .ToList();

    /// <summary>Module assemblies keyed by module name, for example <c>Compliance</c>.</summary>
    public static IReadOnlyDictionary<string, Assembly> Modules { get; } = All
        .Where(a => Contracts.Any(c => c.GetName().Name == a.GetName().Name + ContractsSuffix))
        .ToDictionary(a => a.GetName().Name![Prefix.Length..], StringComparer.Ordinal);

    public static Assembly BuildingBlocksAssembly { get; } = All.Single(a => a.GetName().Name == BuildingBlocksName);

    public static Architecture Architecture { get; } = new ArchLoader().LoadAssemblies([.. All]).Build();

    public static IEnumerable<string> FinGuardReferences(Assembly assembly) => assembly
        .GetReferencedAssemblies()
        .Select(r => r.Name!)
        .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal));

    public static bool IsContracts(string assemblyName) =>
        Contracts.Any(c => c.GetName().Name == assemblyName);
}
