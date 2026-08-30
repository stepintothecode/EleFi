using System.IO;

namespace EleFi.Architecture.Tests.Support;

/// <summary>
/// Locates the repository root by walking up from the test binaries until the solution
/// file appears. Tests that read the folder tree need this; nothing else should.
/// </summary>
internal static class RepositoryRoot
{
    private const string SolutionFileName = "EleFi.slnx";

    /// <summary>The repository root directory.</summary>
    public static DirectoryInfo Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
        {
            dir = dir.Parent;
        }

        return dir ?? throw new DirectoryNotFoundException(
            $"Walked up from {AppContext.BaseDirectory} without finding {SolutionFileName}.");
    }

    /// <summary>The <c>src</c> directory.</summary>
    public static DirectoryInfo Src() => new(Path.Combine(Find().FullName, "src"));

    /// <summary>The <c>tests</c> directory.</summary>
    public static DirectoryInfo Tests() => new(Path.Combine(Find().FullName, "tests"));
}
