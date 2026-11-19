using System.Reflection;
using System.Text.RegularExpressions;
using ArchUnitNET.Domain.Extensions;
using FinGuard.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using static FinGuard.ArchitectureTests.FinGuardAssemblies;

namespace FinGuard.ArchitectureTests;

// Module messaging rules (ADR-0009, D-69, D-70).
public sealed partial class MessagingRulesTests
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void IntegrationEvents_InContractsEventsNamespaces_FollowTheConvention()
    {
        // Passes on an empty set until the first real event exists.
        var violations = Contracts
            .SelectMany(c => c.GetTypes())
            .Where(t => t.Namespace?.EndsWith(IntegrationEventConventions.EventsNamespaceSuffix, StringComparison.Ordinal) == true
                        && !t.IsNested && !IsCompilerGenerated(t))
            .SelectMany(EventConventionViolations)
            .ToList();

        Assert.True(violations.Count == 0, $"Integration events must follow ADR-0009: {string.Join("; ", violations)}");
    }

    [Fact]
    public void ModuleHandlers_Always_DependOnAtMostOneDbContext()
    {
        var violations = Modules.Values
            .SelectMany(m => m.GetTypes())
            .Where(t => t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .Select(t => (Handler: t, Contexts: DbContextDependencies(t)))
            .Where(x => x.Contexts.Count > 1)
            .Select(x => $"{x.Handler.FullName} -> {string.Join(", ", x.Contexts.Select(c => c.Name))}")
            .ToList();

        Assert.True(violations.Count == 0,
            $"A handler owns one DbContext; cross-module effects go through events: {string.Join("; ", violations)}");
    }

    [Fact]
    public void ModuleTypes_SendingCommands_UseInvokeForCurrentTenantAsync()
    {
        var moduleNames = Modules.Values.Select(m => m.GetName().Name).ToHashSet(StringComparer.Ordinal);

        var violations = Architecture.Classes
            .Where(c => moduleNames.Contains(c.Assembly.Name.Split(',')[0]))
            .SelectMany(c => c.GetCalledMethods()
                .Where(m => m.DeclaringType.FullName.StartsWith("Wolverine.", StringComparison.Ordinal)
                            && DirectInvoke().IsMatch(m.Name))
                .Select(m => $"{c.FullName} calls {m.DeclaringType.FullName}.{m.Name}"))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Send commands with {nameof(MessageBusTenantExtensions.InvokeForCurrentTenantAsync)}: {string.Join("; ", violations)}");
    }

    [Fact]
    public void MessageTypes_Always_OverrideToString()
    {
        // R-21 (D-77): Wolverine logs a failed message with ToString(); a record prints every property by default.
        var handled = Modules.Values
            .SelectMany(m => m.GetTypes())
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => HandlerMethodNames.Contains(m.Name) && m.GetParameters().Length > 0)
            .Select(m => m.GetParameters()[0].ParameterType)
            // Messages are FinGuard types; this skips framework handlers such as IAuthorizationMiddlewareResultHandler.
            .Where(t => t.Assembly.GetName().Name!.StartsWith(Prefix, StringComparison.Ordinal));
        var events = Contracts
            .SelectMany(c => c.GetTypes())
            .Where(t => t.Namespace?.EndsWith(IntegrationEventConventions.EventsNamespaceSuffix, StringComparison.Ordinal) == true
                        && !t.IsNested && !IsCompilerGenerated(t));

        var violations = handled.Concat(events)
            .Distinct()
            .Where(t => !OverridesToString(t))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Message types must override ToString() with type name and identifiers only: {string.Join("; ", violations)}");
    }

    private static readonly HashSet<string> HandlerMethodNames = new(StringComparer.Ordinal)
    {
        "Handle", "HandleAsync", "Consume", "ConsumeAsync",
    };

    // A record's synthesized ToString() is declared on the record too, so the compiler-generated marker tells them apart.
    private static bool OverridesToString(Type type) =>
        type.GetMethod(nameof(ToString), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes) is { } method
        && method.DeclaringType == type
        && !method.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);

    private static IEnumerable<string> EventConventionViolations(Type type)
    {
        if (!type.IsSealed || type.GetMethod("<Clone>$") is null)
        {
            yield return $"{type.FullName} is not a sealed record";
        }

        foreach (var (name, propertyType) in new[]
                 {
                     ("EventId", typeof(Guid)), ("TenantId", typeof(Guid)), ("OccurredAt", typeof(DateTimeOffset)),
                 })
        {
            if (type.GetProperty(name)?.PropertyType != propertyType)
            {
                yield return $"{type.FullName} lacks {propertyType.Name} {name}";
            }
        }
    }

    // Constructor parameters, method parameters and fields of the handler: every way Wolverine or a class can get one.
    private static List<Type> DbContextDependencies(Type handler) => handler
        .GetConstructors(Declared).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
        .Concat(handler.GetMethods(Declared).SelectMany(m => m.GetParameters()).Select(p => p.ParameterType))
        .Concat(handler.GetFields(Declared).Select(f => f.FieldType))
        .Where(t => t.IsSubclassOf(typeof(DbContext)))
        .Distinct()
        .ToList();

    private static bool IsCompilerGenerated(Type type) =>
        type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);

    [GeneratedRegex(@"^Invoke(ForTenant)?Async\b")]
    private static partial Regex DirectInvoke();
}
