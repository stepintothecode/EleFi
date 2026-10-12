using EleFi.Domain.Primitives;

namespace EleFi.Domain.Parties;

/// <summary>Which side of the boundary a party sits on.</summary>
public enum PartyKind
{
    /// <summary>A Money Container the user owns.</summary>
    Container = 0,

    /// <summary>Anyone or anything outside the user's money: a person, a merchant, an employer.</summary>
    External = 1,
}

/// <summary>
/// Either end of a transaction: where money came from, or where it went.
/// </summary>
/// <remarks>
/// Internal and external parties share one table because Source and Destination are the
/// same <em>role</em> whether the far end is a shop or the user's own wallet. That is what
/// makes a single shared suggestion pool fall out of the model rather than being built.
/// </remarks>
public class Party : Entity
{
    /// <summary>Whether this is one of the user's containers or someone outside.</summary>
    public PartyKind Kind { get; set; }

    /// <summary>The container, when <see cref="Kind"/> is Container. Null otherwise (P1, P2).</summary>
    public Guid? ContainerId { get; set; }

    /// <summary>The name, when <see cref="Kind"/> is External. Null otherwise (P1, P2).</summary>
    public string? Name { get; set; }

    /// <summary>How often this party has been used. Ranks suggestions; never touches a balance.</summary>
    public int UsageCount { get; set; }

    /// <summary>When it was last used. Ranks suggestions.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>What the user has written about this contact. External parties only.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// The phone's own key for the contact this one was linked to, so it can be opened in the
    /// phone's Contacts app. Not a copy of that contact: only what the user picked is kept.
    /// </summary>
    public string? PhoneContactKey { get; set; }

    /// <summary>
    /// The contact this one was merged into, or renamed to, when this row is an
    /// <see cref="IsAlias">alias</see> (CT4).
    /// </summary>
    public Guid? MergedIntoPartyId { get; set; }

    /// <summary>True when this party is one of the user's own containers.</summary>
    public bool IsInternal => Kind == PartyKind.Container;

    /// <summary>
    /// True when this row is only another name for a live contact: one merged away, or a name
    /// kept from before a rename. It is soft-deleted and appears in no list, but capturing
    /// with its name lands on the contact it points at.
    /// </summary>
    public bool IsAlias => DeletedAt is not null && MergedIntoPartyId is not null;

    /// <summary>
    /// Normalises an external party name so "Rahul", "rahul " and "RAHUL" are one party
    /// and the suggestion list does not fragment (P4).
    /// </summary>
    /// <param name="name">The raw name.</param>
    public static string NormaliseName(string name) => name.Trim();
}
