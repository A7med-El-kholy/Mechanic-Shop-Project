using System.Reflection;
using AwesomeAssertions;

namespace MechanicShop.Application.UnitTests.Architecture;

/// <summary>
/// Guards the Clean Architecture rule for the Application assembly:
/// it may depend on the Domain assembly and the base class library —
/// nothing else. Framework and infrastructure concerns must be expressed
/// as interfaces declared in Application and implemented elsewhere.
/// </summary>
public sealed class ApplicationAssemblyTests
{
    [Fact]
    public void ApplicationAssembly_DependsOnlyOnDomainAndBaseClassLibrary()
    {
        var applicationAssembly = Assembly.Load("MechanicShop.Application");

        var disallowed = applicationAssembly
            .GetReferencedAssemblies()
            .Where(reference => !IsAllowed(reference.Name))
            .Select(reference => reference.Name ?? "<null-name>")
            .ToArray();

        disallowed.Should().BeEmpty(
            "Application may reference only MechanicShop.Domain and the base class library");
    }

    private static bool IsAllowed(string? assemblyName) =>
        assemblyName is not null
        && (assemblyName == "MechanicShop.Domain"
            || assemblyName.StartsWith("System", StringComparison.Ordinal)
            || assemblyName is "netstandard" or "mscorlib");
}
