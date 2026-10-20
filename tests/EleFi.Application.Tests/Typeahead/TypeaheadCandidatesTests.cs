using EleFi.Application.Typeahead;
using EleFi.Domain.Apps;
using EleFi.Domain.Parties;

namespace EleFi.Application.Tests.Typeahead;

public class TypeaheadCandidatesTests
{
    [Fact]
    public void Container_parties_are_never_offered_as_a_name_to_type()
    {
        var used = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

        var candidates = TypeaheadCandidates.From(
        [
            new Party { Kind = PartyKind.Container, ContainerId = Guid.NewGuid() },
            new Party { Kind = PartyKind.External, Name = "Zomato", UsageCount = 4, LastUsedAt = used },
            new Party { Kind = PartyKind.External, Name = "  " },
        ]);

        Assert.Equal([new TypeaheadCandidate("Zomato", 4, used)], candidates);
    }

    [Fact]
    public void Apps_carry_their_usage_across()
    {
        var candidates = TypeaheadCandidates.From([new App { Name = "GPay", UsageCount = 7 }]);

        Assert.Equal([new TypeaheadCandidate("GPay", 7, null)], candidates);
    }

    [Fact]
    public void An_earlier_list_always_ranks_above_a_later_one_whatever_the_usage()
    {
        var withThisContact = new[] { new TypeaheadCandidate("Swiggy", 1, null) };
        var mostUsedOverall = new[] { new TypeaheadCandidate("Zomato", 90, null) };

        var merged = TypeaheadCandidates.InTiers(withThisContact, mostUsedOverall);

        Assert.Equal(["Swiggy", "Zomato"], TypeaheadRanker.Rank(merged, null, 3));
    }

    [Fact]
    public void Within_a_list_usage_still_decides()
    {
        var merged = TypeaheadCandidates.InTiers(
            [new TypeaheadCandidate("GPay", 2, null), new TypeaheadCandidate("Cash", 7, null)],
            [new TypeaheadCandidate("PhonePe", 50, null)]);

        Assert.Equal(["Cash", "GPay", "PhonePe"], TypeaheadRanker.Rank(merged, null, 3));
    }

    [Fact]
    public void A_name_in_two_lists_keeps_its_place_in_the_earlier()
    {
        var merged = TypeaheadCandidates.InTiers(
            [new TypeaheadCandidate("GPay", 1, null)],
            [new TypeaheadCandidate("gpay", 80, null), new TypeaheadCandidate("Cash", 3, null)]);

        Assert.Equal(2, merged.Count);
        Assert.Equal(["GPay", "Cash"], TypeaheadRanker.Rank(merged, null, 3));
    }
}
