using System.Reflection;

namespace BuffeRR.UnitTests;

/// <summary>
/// Enforces the dependency rule from the README: dependencies point inward, Core depends on nothing.
/// Note: the compiler only records an assembly reference when the code actually uses a type from it,
/// so these tests catch real coupling, not merely declared project references.
/// </summary>
public sealed class ArchitectureTests
{
    private const string ProductPrefix = "BuffeRR.";

    // (assembly, product assemblies it may reference). Everything else under BuffeRR.* is a violation.
    private static readonly (Assembly Assembly, string[] Allowed)[] Rules =
    [
        (typeof(Core.AssemblyMarker).Assembly, []),
        (typeof(Engine.AssemblyMarker).Assembly, ["BuffeRR.Core"]),
        (typeof(Persistence.AssemblyMarker).Assembly, ["BuffeRR.Core"]),
        (typeof(Protocol.AssemblyMarker).Assembly, ["BuffeRR.Core"]),
        (typeof(Transport.AssemblyMarker).Assembly, ["BuffeRR.Core"]),
        (typeof(Server.AssemblyMarker).Assembly,
            ["BuffeRR.Core", "BuffeRR.Engine", "BuffeRR.Persistence", "BuffeRR.Protocol", "BuffeRR.Transport"]),
        (typeof(Client.AssemblyMarker).Assembly, ["BuffeRR.Core", "BuffeRR.Protocol", "BuffeRR.Transport"]),
    ];

    [Fact]
    public void Layers_reference_only_the_product_assemblies_the_architecture_allows()
    {
        var violations = new List<string>();

        foreach (var (assembly, allowed) in Rules)
        {
            var self = assembly.GetName().Name!;
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                var name = reference.Name!;
                var isProductAssembly = name.StartsWith(ProductPrefix, StringComparison.Ordinal);
                if (isProductAssembly && name != self && !allowed.Contains(name))
                    violations.Add($"{self} -> {name}");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Core_references_only_the_base_class_library()
    {
        var coreAssembly = typeof(Core.AssemblyMarker).Assembly;

        var nonFramework = coreAssembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !n.StartsWith("System", StringComparison.Ordinal)
                        && !n.StartsWith("Microsoft", StringComparison.Ordinal)
                        && n != "netstandard"
                        && n != "mscorlib")
            .ToList();

        Assert.Empty(nonFramework);
    }
}
