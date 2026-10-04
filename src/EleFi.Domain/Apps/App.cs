using EleFi.Domain.Primitives;

namespace EleFi.Domain.Apps;

/// <summary>
/// A service, portal, or platform involved in a transaction: Zomato, GPay, Cash.
/// </summary>
/// <remarks>
/// <para>
/// One kind of thing appearing in two roles: the Marketplace App is what was bought
/// through, the Payment App is the rail money moved along. Both draw from this one pool,
/// so a name typed in either place is suggested in both afterwards. There is no role field
/// because "Cash" is legitimately both, and forcing a choice at creation time only creates
/// duplicates (A2).
/// </para>
/// <para>
/// An App never participates in a balance calculation (A3). The source container is the
/// sole truth about where money left; the payment app only describes how. A wrong or
/// missing App is a cosmetic error, and that is the entire justification for keeping this
/// separate from Container.
/// </para>
/// </remarks>
public class App : Entity
{
    /// <summary>The name, unique per user after trimming and case folding (A1).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The container to pre-fill when this app is chosen, so "GPay" fills in HDFC.</summary>
    public Guid? DefaultContainerId { get; set; }

    /// <summary>An icon name for the UI.</summary>
    public string? Icon { get; set; }

    /// <summary>A hex colour for the UI.</summary>
    public string? Colour { get; set; }

    /// <summary>How often it has been used. Ranks suggestions only.</summary>
    public int UsageCount { get; set; }

    /// <summary>When it was last used. Ranks suggestions only.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Normalises an app name so casing does not fragment the pool.</summary>
    /// <param name="name">The raw name.</param>
    public static string Normalise(string name) => name.Trim();
}
