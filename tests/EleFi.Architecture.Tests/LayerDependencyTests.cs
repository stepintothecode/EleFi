using System.Linq;
using System.Reflection;
using NetArchTest.Rules;

namespace EleFi.Architecture.Tests;

/// <summary>
/// The dependency rule from software-development.md section 2, enforced rather than
/// trusted. Discipline is what erodes at week six.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly Domain = EleFi.Domain.AssemblyMarker.Assembly;
    private static readonly Assembly Application = EleFi.Application.AssemblyMarker.Assembly;
    private static readonly Assembly Infrastructure = EleFi.Infrastructure.AssemblyMarker.Assembly;

    private static string[] ReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
                .Select(a => a.Name ?? string.Empty)
                .ToArray();

    // Checking referenced assemblies, not just used types, catches a ProjectReference
    // added "temporarily" before anything has used it. That is when it is cheap to undo.

    [Fact]
    public void Domain_references_nothing_but_the_bcl()
    {
        var forbidden = ReferencesOf(Domain)
            .Where(n => n.StartsWith("EleFi.", StringComparison.Ordinal)
                     || n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                     || n.StartsWith("Microsoft.Maui", StringComparison.Ordinal)
                     || n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                     || n.StartsWith("SQLitePCLRaw", StringComparison.Ordinal)
                     || n.StartsWith("Google.Apis", StringComparison.Ordinal)
                     || n.StartsWith("Mono.Android", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            forbidden.Length == 0,
            $"EleFi.Domain must reference nothing but the BCL. Found: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void Application_references_only_domain()
    {
        var forbidden = ReferencesOf(Application)
            .Where(n => n.StartsWith("EleFi.", StringComparison.Ordinal)
                     && !string.Equals(n, "EleFi.Domain", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            forbidden.Length == 0,
            $"EleFi.Application may reference EleFi.Domain only. Found: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void Application_holds_no_persistence_or_platform_reference()
    {
        var forbidden = ReferencesOf(Application)
            .Where(n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                     || n.StartsWith("Microsoft.Maui", StringComparison.Ordinal)
                     || n.StartsWith("SQLitePCLRaw", StringComparison.Ordinal)
                     || n.StartsWith("Google.Apis", StringComparison.Ordinal)
                     || n.StartsWith("Mono.Android", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            forbidden.Length == 0,
            $"EleFi.Application declares ports; it does not implement them. Found: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void Infrastructure_does_not_reference_the_ui()
    {
        var forbidden = ReferencesOf(Infrastructure)
            .Where(n => string.Equals(n, "EleFi.Ui", StringComparison.Ordinal)
                     || string.Equals(n, "EleFi.App", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            forbidden.Length == 0,
            $"Dependencies point inward. Found: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void Domain_types_do_not_depend_on_outer_layers()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny("EleFi.Application", "EleFi.Infrastructure", "EleFi.Ui", "EleFi.App")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_types_do_not_depend_on_outer_layers()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("EleFi.Infrastructure", "EleFi.Ui", "EleFi.App")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        result.FailingTypeNames is null
            ? "Rule failed."
            : $"Offending types: {string.Join(", ", result.FailingTypeNames)}";
}
