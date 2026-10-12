using EleFi.Domain.Primitives;

namespace EleFi.Domain.Parties;

/// <summary>How two linked contacts are related.</summary>
public enum ContactRelation
{
    /// <summary>The same person or business, kept as two contacts: "Amazon" and "Amazon Pay India".</summary>
    SameAs = 0,

    /// <summary>
    /// Another account of the same person or business: Rahul's HDFC UPI and his SBI UPI.
    /// </summary>
    AnotherAccountOf = 1,

    /// <summary>
    /// The user's answer to a duplicate suggestion: these two are different people. Joins
    /// nothing; it only stops the pair being suggested again.
    /// </summary>
    NotTheSame = 2,
}

/// <summary>Reading a relation.</summary>
public static class ContactRelations
{
    /// <summary>True for a relation that joins two contacts, false for "not the same".</summary>
    /// <param name="relation">The relation.</param>
    public static bool Joins(this ContactRelation relation) => relation != ContactRelation.NotTheSame;

    /// <summary>The relation as a contact page says it.</summary>
    /// <param name="relation">The relation.</param>
    public static string DisplayName(this ContactRelation relation) => relation switch
    {
        ContactRelation.SameAs => "Same as",
        ContactRelation.AnotherAccountOf => "Another account of",
        _ => "Not the same as",
    };
}

/// <summary>
/// Two contacts kept apart but connected: the same person under two names, or two accounts
/// of the same business.
/// </summary>
/// <remarks>
/// <para>
/// A link joins, a merge combines. Linked contacts keep their own names, details and
/// transactions, and each contact's page lists the other; a merge folds one into the other
/// for good (CT4). Linking is the answer when the two really are kept separately somewhere,
/// such as two bank accounts.
/// </para>
/// <para>
/// Undirected, and stored with its two ends in a fixed order, so linking A to B and B to A is
/// one link (CT3). A contact is never linked to itself.
/// </para>
/// </remarks>
public class ContactLink : Entity
{
    /// <summary>One end: the lower id of the two.</summary>
    public Guid PartyId { get; set; }

    /// <summary>The other end: the higher id.</summary>
    public Guid OtherPartyId { get; set; }

    /// <summary>How they are related.</summary>
    public ContactRelation Relation { get; set; }

    /// <summary>A link between two different contacts, ends in their fixed order.</summary>
    /// <param name="one">One contact.</param>
    /// <param name="other">The other.</param>
    /// <param name="relation">How they are related.</param>
    /// <param name="now">The creation instant.</param>
    /// <exception cref="ArgumentException">Both ends are the same contact.</exception>
    public static ContactLink Between(Guid one, Guid other, ContactRelation relation, DateTimeOffset now)
    {
        if (one == other)
        {
            throw new ArgumentException("A contact cannot be linked to itself.", nameof(other));
        }

        var (low, high) = one.CompareTo(other) < 0 ? (one, other) : (other, one);

        return new ContactLink
        {
            PartyId = low,
            OtherPartyId = high,
            Relation = relation,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>True when this link joins exactly these two, either way round.</summary>
    /// <param name="one">One contact.</param>
    /// <param name="other">The other.</param>
    public bool Joins(Guid one, Guid other) =>
        (PartyId == one && OtherPartyId == other) || (PartyId == other && OtherPartyId == one);

    /// <summary>The end that is not this one.</summary>
    /// <param name="end">One end of the link.</param>
    public Guid Other(Guid end) => end == PartyId ? OtherPartyId : PartyId;
}
