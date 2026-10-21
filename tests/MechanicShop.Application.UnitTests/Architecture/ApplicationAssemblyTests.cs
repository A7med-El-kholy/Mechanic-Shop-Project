using System.Reflection;
using AwesomeAssertions;

namespace MechanicShop.Application.UnitTests.Architecture;

/// <summary>
/// Guards the Clean Architecture rule for the Application assembly: its references
/// must stay inside a deliberate allowlist — Domain, the base class library, and the
/// small set of LOW-LEVEL contracts the use-cases genuinely need:
///
///   - Microsoft.*: EF Core ABSTRACTIONS (DbSet/LINQ/DbUpdateException for the
///     IAppDbContext contract) and Microsoft.Extensions logging abstractions.
///     The SQL Server provider stays out — handlers never touch a concrete database.
///   - MediatR(.Contracts): commands/queries are dispatched as messages; this is
///     the CQRS mediation primitive, not a framework dependency.
///   - FluentValidation(.DependencyInjectionExtensions): request-shape validators.
///
/// Everything else — Identity, JWT, HTTP, SQL provider — must stay expressed as
/// interfaces implemented in Infrastructure/Api. A new reference appearing here is
/// a design smell and must be decided deliberately in this list.
/// </summary>
public sealed class ApplicationAssemblyTests
{
    [Fact]
    public void ApplicationAssembly_DependsOnlyOnAllowedReferences()
    {
        var applicationAssembly = Assembly.Load("MechanicShop.Application");

        var disallowed = applicationAssembly
            .GetReferencedAssemblies()
            .Where(reference => !IsAllowed(reference.Name))
            .Select(reference => reference.Name ?? "<null-name>")
            .ToArray();

        disallowed.Should().BeEmpty(
            "Application references must stay inside the deliberate allowlist");
    }

    private static bool IsAllowed(string? assemblyName) =>
        assemblyName is not null
        && (assemblyName == "MechanicShop.Domain"
            || assemblyName.StartsWith("System", StringComparison.Ordinal)
            || assemblyName.StartsWith("Microsoft.Extensions", StringComparison.Ordinal)
            || assemblyName == "Microsoft.EntityFrameworkCore"
            || assemblyName == "Microsoft.EntityFrameworkCore.Relational"
            || assemblyName.StartsWith("MediatR", StringComparison.Ordinal)
            || assemblyName.StartsWith("FluentValidation", StringComparison.Ordinal)
            // Microsoft.CSharp: the binder assembly required by the (dynamic) cast
            // inside ValidationBehavior when converting validator failures to List<Error>.
            || assemblyName == "Microsoft.CSharp"
            || assemblyName is "netstandard" or "mscorlib");
}
