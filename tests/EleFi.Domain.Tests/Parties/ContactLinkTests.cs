using EleFi.Domain.Parties;

namespace EleFi.Domain.Tests.Parties;

/// <summary>CT3: a link joins two different contacts, once, whichever way round it was made.</summary>
public class ContactLinkTests
{
    private static readonly Guid Rahul = Guid.CreateVersion7();
    private static readonly Guid RahulSbi = Guid.CreateVersion7();

    [Fact]
    public void A_link_is_the_same_link_whichever_way_round_it_is_made()
    {
        var one = ContactLink.Between(Rahul, RahulSbi, ContactRelation.AnotherAccountOf, DateTimeOffset.UnixEpoch);
        var other = ContactLink.Between(RahulSbi, Rahul, ContactRelation.AnotherAccountOf, DateTimeOffset.UnixEpoch);

        Assert.Equal((one.PartyId, one.OtherPartyId), (other.PartyId, other.OtherPartyId));
    }

    [Fact]
    public void Each_end_finds_the_other()
    {
        var link = ContactLink.Between(Rahul, RahulSbi, ContactRelation.SameAs, DateTimeOffset.UnixEpoch);

        Assert.Equal(RahulSbi, link.Other(Rahul));
        Assert.Equal(Rahul, link.Other(RahulSbi));
        Assert.True(link.Joins(Rahul, RahulSbi));
    }

    [Fact]
    public void A_contact_cannot_be_linked_to_itself()
    {
        Assert.Throws<ArgumentException>(() => ContactLink.Between(Rahul, Rahul, ContactRelation.SameAs, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Not_the_same_is_a_relation_that_says_so_without_joining_them()
    {
        Assert.False(ContactRelation.NotTheSame.Joins());
        Assert.True(ContactRelation.SameAs.Joins());
        Assert.True(ContactRelation.AnotherAccountOf.Joins());
    }
}
