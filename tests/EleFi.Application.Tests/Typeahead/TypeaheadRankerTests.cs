using EleFi.Application.Typeahead;

namespace EleFi.Application.Tests.Typeahead;

/// <summary>
/// The ranking behind "Paid to": most used first when nothing is typed, best match first once
/// something is.
/// </summary>
public class TypeaheadRankerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void With_nothing_typed_the_most_used_come_first()
    {
        var ranked = TypeaheadRanker.Rank(
        [
            new("Rahul", 2, Now),
            new("Zomato", 9, Now.AddDays(-3)),
            new("Swiggy", 5, Now.AddDays(-1)),
            new("Landlord", 1, Now),
        ], query: null, take: 3);

        Assert.Equal(["Zomato", "Swiggy", "Rahul"], ranked);
    }

    [Fact]
    public void Equal_usage_is_broken_by_recency()
    {
        var ranked = TypeaheadRanker.Rank(
        [
            new("Old", 3, Now.AddDays(-30)),
            new("Fresh", 3, Now),
        ], query: "", take: 3);

        Assert.Equal(["Fresh", "Old"], ranked);
    }

    [Fact]
    public void A_prefix_match_beats_a_more_used_match_further_inside_the_name()
    {
        var ranked = TypeaheadRanker.Rank(
        [
            new("Big Bazaar", 40, Now),
            new("Bazaar Kolkata", 1, Now),
        ], query: "baz", take: 3);

        // "baz" is what someone types when they mean the name that starts with it.
        Assert.Equal("Bazaar Kolkata", ranked[0]);
        Assert.Equal("Big Bazaar", ranked[1]);
    }

    [Fact]
    public void A_word_prefix_beats_a_match_in_the_middle_of_a_word()
    {
        var ranked = TypeaheadRanker.Rank(
        [
            new("Jumbo Rolls", 9, Now),
            new("Mumbai Rolls", 1, Now),
        ], query: "rol", take: 3);

        Assert.Equal(2, ranked.Count);

        // Both are word prefixes, so usage decides. A plain substring match would rank
        // below both.
        Assert.Equal("Jumbo Rolls", ranked[0]);
    }

    [Fact]
    public void Matching_ignores_case_and_surrounding_spaces()
    {
        var ranked = TypeaheadRanker.Rank([new("Zomato", 1, Now)], query: "  ZOM ", take: 3);

        Assert.Equal(["Zomato"], ranked);
    }

    [Fact]
    public void Every_name_in_history_is_searched_not_just_the_recent_ones()
    {
        // The old picker handed the browser the twenty most recent and let it filter those,
        // so anyone not paid lately could not be found by typing.
        var history = Enumerable.Range(0, 500)
            .Select(i => new TypeaheadCandidate($"Merchant {i:000}", 500 - i, Now.AddDays(-i)))
            .Append(new TypeaheadCandidate("Chaiwala", 0, Now.AddYears(-2)))
            .ToList();

        var ranked = TypeaheadRanker.Rank(history, query: "chai", take: 3);

        Assert.Equal(["Chaiwala"], ranked);
    }

    [Fact]
    public void Letters_in_order_still_find_a_name_when_nothing_matches_more_closely()
    {
        var ranked = TypeaheadRanker.Rank(
        [
            new("Zomato", 1, Now),
            new("Amazon", 1, Now),
        ], query: "zmt", take: 3);

        // A dropped vowel while typing one-handed should not mean "no results".
        Assert.Equal(["Zomato"], ranked);
    }

    [Fact]
    public void Letters_in_order_are_not_used_for_very_short_queries()
    {
        var ranked = TypeaheadRanker.Rank(
        [
            new("Zomato", 1, Now),
            new("Amazon", 1, Now),
        ], query: "zn", take: 3);

        // Two letters scattered through a name match almost everything, which is noise.
        Assert.Empty(ranked);
    }

    [Fact]
    public void Names_differing_only_by_case_appear_once()
    {
        var ranked = TypeaheadRanker.Rank(
        [
            new("zomato", 1, Now),
            new("Zomato", 5, Now),
        ], query: null, take: 3);

        Assert.Equal(["Zomato"], ranked);
    }

    [Fact]
    public void Blank_names_are_never_offered()
    {
        var ranked = TypeaheadRanker.Rank(
        [
            new("", 9, Now),
            new("   ", 9, Now),
            new("Rahul", 1, Now),
        ], query: null, take: 3);

        Assert.Equal(["Rahul"], ranked);
    }

    [Fact]
    public void Take_caps_the_result()
    {
        var ranked = TypeaheadRanker.Rank(
            Enumerable.Range(0, 10).Select(i => new TypeaheadCandidate($"Shop {i}", i, Now)),
            query: "shop",
            take: 3);

        Assert.Equal(3, ranked.Count);
    }

    [Fact]
    public void A_take_of_zero_returns_nothing()
    {
        Assert.Empty(TypeaheadRanker.Rank([new("Rahul", 1, Now)], query: null, take: 0));
    }
}
