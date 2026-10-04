using System.Collections.Generic;
using System.IO;
using System.Linq;
using EleFi.Architecture.Tests.Support;

namespace EleFi.Architecture.Tests;

/// <summary>
/// Keeps the folder tree navigable: every project under <c>src</c> has a test project
/// under <c>tests</c> with a matching name, and every test file sits at the same relative
/// path as the source file it covers.
/// <para>
/// The point is that finding the tests for a file never needs a search.
/// </para>
/// </summary>
public class RepositoryLayoutTests
{
    /// <summary>
    /// Projects that have no test project yet, with the reason. Removing a name from here
    /// is how a project becomes covered; adding one needs a reason that survives review.
    /// </summary>
    private static readonly Dictionary<string, string> ProjectsWithoutTests = new()
    {
        ["EleFi.App"] = "MAUI host shell: platform wiring only, exercised by the Appium suite. "
                      + "Anything testable belongs in EleFi.Ui.",
    };

    /// <summary>
    /// Test projects that deliberately have no counterpart in <c>src</c>, because they test
    /// the repository rather than one assembly. Keep this list short: a cross-cutting test
    /// project is a real cost to navigation and needs to earn its place.
    /// </summary>
    private static readonly Dictionary<string, string> CrossCuttingTestProjects = new()
    {
        ["EleFi.Architecture"] = "Tests the repository itself: layer boundaries and folder layout.",
        ["EleFi.Properties"] = "Universal properties spanning Domain, Application and Infrastructure.",
    };

    /// <summary>
    /// Test files that deliberately have no single source file behind them, with the reason.
    /// </summary>
    /// <remarks>
    /// Keep this short. A test that mirrors nothing is a test nobody will find when they
    /// change the thing it covers, so each entry has to be genuinely cross-cutting rather
    /// than merely awkward to name.
    /// </remarks>
    private static readonly Dictionary<string, string> CrossCuttingTestFiles = new()
    {
        ["EleFi.Infrastructure.Tests/Persistence/MigrationSafetyTests.cs"] =
            "Covers the migration set as a whole. Migration files carry generated timestamp "
            + "prefixes, so no test filename could mirror one without being renamed every time "
            + "a migration is added.",
    };

    private static readonly string[] IgnoredDirectories = ["bin", "obj", "Platforms", "Resources", "wwwroot", "Properties"];

    [Fact]
    public void Every_src_project_has_a_matching_test_project()
    {
        var missing = SrcProjectNames()
            .Where(name => !ProjectsWithoutTests.ContainsKey(name))
            .Where(name => !Directory.Exists(Path.Combine(RepositoryRoot.Tests().FullName, $"{name}.Tests")))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"Expected tests/<name>.Tests for: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Every_test_project_matches_a_src_project()
    {
        var src = SrcProjectNames().ToHashSet(StringComparer.Ordinal);

        var orphans = RepositoryRoot.Tests()
            .GetDirectories()
            .Select(d => d.Name)
            .Where(n => n.EndsWith(".Tests", StringComparison.Ordinal))
            .Select(n => n[..^".Tests".Length])
            .Where(n => !src.Contains(n))
            .Where(n => !CrossCuttingTestProjects.ContainsKey(n))
            .ToArray();

        Assert.True(
            orphans.Length == 0,
            $"Test projects with no project in src/: {string.Join(", ", orphans)}. "
            + "Either add the project, or list it in CrossCuttingTestProjects with a reason.");
    }

    [Fact]
    public void Every_test_file_mirrors_the_path_of_the_file_it_covers()
    {
        var offenders = new List<string>();

        foreach (var testProject in RepositoryRoot.Tests().GetDirectories("*.Tests"))
        {
            var sourceProjectName = testProject.Name[..^".Tests".Length];
            if (CrossCuttingTestProjects.ContainsKey(sourceProjectName))
            {
                continue;
            }

            var sourceProject = new DirectoryInfo(Path.Combine(RepositoryRoot.Src().FullName, sourceProjectName));
            if (!sourceProject.Exists)
            {
                continue;
            }

            foreach (var testFile in EnumerateCode(testProject).Where(f => f.Name.EndsWith("Tests.cs", StringComparison.Ordinal)))
            {
                var relative = Path.GetRelativePath(testProject.FullName, testFile.FullName);

                var key = $"{testProject.Name}/{relative.Replace(Path.DirectorySeparatorChar, '/')}";
                if (CrossCuttingTestFiles.ContainsKey(key))
                {
                    continue;
                }

                // Foo/BarTests.cs covers Foo/Bar.cs (or Foo/Bar.razor).
                var expectedStem = relative[..^"Tests.cs".Length];
                var candidates = new[] { expectedStem + ".cs", expectedStem + ".razor" }
                    .Select(c => Path.Combine(sourceProject.FullName, c));

                if (!candidates.Any(File.Exists))
                {
                    offenders.Add($"{testProject.Name}/{relative} has no matching source file in src/{sourceProjectName}/");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }

    private static IEnumerable<string> SrcProjectNames() =>
        RepositoryRoot.Src().GetDirectories().Select(d => d.Name);

    private static IEnumerable<FileInfo> EnumerateCode(DirectoryInfo root) =>
        root.EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Split(Path.DirectorySeparatorChar).Any(IgnoredDirectories.Contains));
}
