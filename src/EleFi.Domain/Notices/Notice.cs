using EleFi.Domain.Primitives;

namespace EleFi.Domain.Notices;

/// <summary>
/// One line in the in-app notification list: something EleFi did on the user's behalf.
/// </summary>
/// <remarks>
/// <para>
/// A system notification is gone once swiped away. This is the record that stays: every
/// payment recorded from an SMS or a payment app, and every plan ticked off by one, readable
/// in full later and marked read or unread by the user.
/// </para>
/// <para>
/// It holds only what EleFi wrote, never the text of the message that caused it (SM1).
/// </para>
/// </remarks>
public class Notice : Entity
{
    /// <summary>The headline: "₹450 to Zomato".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The full detail.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Where "Open" goes, as a base-relative route, or null when there is nowhere to go.</summary>
    public string? Route { get; set; }

    /// <summary>When the user marked it read, or null while unread.</summary>
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>True once read.</summary>
    public bool IsRead => ReadAt is not null;
}
