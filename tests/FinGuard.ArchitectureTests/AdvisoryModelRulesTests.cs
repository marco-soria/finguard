using System.Reflection;
using FinGuard.Advisory.Features.Refinement;
using Microsoft.EntityFrameworkCore;
using static FinGuard.ArchitectureTests.FinGuardAssemblies;

namespace FinGuard.ArchitectureTests;

// The model narrator's boundaries (ADR-0006, ADR-0015).
public sealed class AdvisoryModelRulesTests
{
    private const string AdvisoryAssembly = "FinGuard.Advisory";
    private const string InfrastructureNamespace = "FinGuard.Advisory.Infrastructure";

    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly string[] ModelPackagePrefixes =
        ["Microsoft.Agents.", "OllamaSharp", "CommunityToolkit.Aspire.OllamaSharp", "Microsoft.Extensions.AI"];

    [Fact]
    public void Assemblies_OtherThanAdvisory_DoNotReferenceModelPackages()
    {
        var violations = All
            .Where(a => a.GetName().Name != AdvisoryAssembly)
            .SelectMany(a => a.GetReferencedAssemblies()
                .Where(r => IsModelPackage(r.Name!))
                .Select(r => $"{a.GetName().Name} -> {r.Name}"))
            .ToList();

        Assert.True(violations.Count == 0, $"Only FinGuard.Advisory may use model packages: {string.Join("; ", violations)}");
    }

    [Fact]
    public void AdvisoryTypes_OutsideInfrastructure_DoNotUseModelTypes()
    {
        var advisoryTypes = Architecture.Types.Where(t => t.Assembly.Name.Split(',')[0] == AdvisoryAssembly).ToList();
        // Not vacuous: the adapter itself does depend on Agent Framework types.
        Assert.Contains(advisoryTypes, t => t.Namespace.FullName == InfrastructureNamespace
                                            && t.Dependencies.Any(d => IsModelPackage(d.Target.FullName)));

        var violations = advisoryTypes
            .Where(t => !t.Namespace.FullName.StartsWith(InfrastructureNamespace, StringComparison.Ordinal))
            .SelectMany(t => t.Dependencies
                .Where(d => IsModelPackage(d.Target.FullName))
                .Select(d => $"{t.FullName} -> {d.Target.FullName}"))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Agent Framework and Ollama types belong in {InfrastructureNamespace}: {string.Join("; ", violations)}");
    }

    [Fact]
    public void RefineNarrativeHandler_Always_HasNoDbContextDependency()
    {
        // Pins "the model never runs inside a transaction": Wolverine opens one only for handlers that use a DbContext.
        var handler = typeof(RefineNarrativeHandler);
        var dependencies = handler.GetConstructors(Declared).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
            .Concat(handler.GetMethods(Declared).SelectMany(m => m.GetParameters()).Select(p => p.ParameterType))
            .Concat(handler.GetFields(Declared).Select(f => f.FieldType));

        Assert.DoesNotContain(dependencies, t => t.IsSubclassOf(typeof(DbContext)));
    }

    [Fact]
    public void AdvisoryMessages_ToString_CarryNoText()
    {
        const string Secret = "SECRET-NARRATIVE-TEXT";
        var messages = Modules["Advisory"].GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m.Name == "Handle" && m.GetParameters().Length > 0)
            .Select(m => m.GetParameters()[0].ParameterType)
            .Where(t => t.Assembly.GetName().Name == AdvisoryAssembly)
            .Distinct();

        var violations = messages
            .Select(t => (Type: t, Text: Instantiate(t, Secret).ToString()!))
            .Where(x => x.Text.Contains(Secret, StringComparison.Ordinal))
            .Select(x => x.Type.FullName)
            .ToList();

        Assert.True(violations.Count == 0, $"Message ToString() must carry identifiers only: {string.Join("; ", violations)}");
    }

    private static bool IsModelPackage(string name) =>
        ModelPackagePrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));

    // The widest constructor, every string set to the marker and every other value to its default.
    private static object Instantiate(Type type, string marker)
    {
        var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var arguments = constructor.GetParameters()
            .Select(p => p.ParameterType == typeof(string) ? marker
                : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)
            .ToArray();
        return constructor.Invoke(arguments);
    }
}
