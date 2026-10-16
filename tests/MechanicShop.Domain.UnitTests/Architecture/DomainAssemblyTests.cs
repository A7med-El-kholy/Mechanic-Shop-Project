using System.Reflection;
using AwesomeAssertions;

namespace MechanicShop.Domain.UnitTests.Architecture;

/// <summary>
/// Guards the Clean Architecture rule that the Domain assembly has NO
/// dependencies: no third-party packages, no other project references.
/// If one is ever added to MechanicShop.Domain, this test fails and names
/// the offending assembly.
/// </summary>
public sealed class DomainAssemblyTests
{
    [Fact]
    public void DomainAssembly_DependsOnlyOnBaseClassLibrary()
    {
        var domainAssembly = Assembly.Load("MechanicShop.Domain");

        var disallowed = domainAssembly
            .GetReferencedAssemblies()
            .Where(reference => !IsBaseClassLibrary(reference.Name))
            .Select(reference => reference.Name ?? "<null-name>")
            .ToArray();

        disallowed.Should().BeEmpty(
            "the Domain assembly must not depend on any third-party or project assemblies");
    }

    private static bool IsBaseClassLibrary(string? assemblyName) =>
        assemblyName is not null
        && (assemblyName.StartsWith("System", StringComparison.Ordinal)
            || assemblyName is "netstandard" or "mscorlib");
}
