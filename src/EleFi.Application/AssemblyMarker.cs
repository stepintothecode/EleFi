namespace EleFi.Application;

/// <summary>
/// Anchors <c>typeof(AssemblyMarker).Assembly</c> for the architecture tests, so they
/// never depend on a feature type that might later be renamed or moved.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>The assembly this marker belongs to.</summary>
    public static System.Reflection.Assembly Assembly => typeof(AssemblyMarker).Assembly;
}
