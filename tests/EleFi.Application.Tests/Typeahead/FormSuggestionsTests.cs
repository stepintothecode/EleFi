using EleFi.Application.Abstractions;
using EleFi.Application.Typeahead;
using EleFi.Domain.Apps;
using NSubstitute;

namespace EleFi.Application.Tests.Typeahead;

/// <summary>
/// Suggestions for "Bought through", "Paid with" and "Note": this contact's history first,
/// then the most used overall, then for apps every app ever named.
/// </summary>
public class FormSuggestionsTests
{
    private readonly ISuggestionHistory _history = Substitute.For<ISuggestionHistory>();
    private readonly IAppRepository _apps = Substitute.For<IAppRepository>();

    public FormSuggestionsTests()
    {
        _history.NotesAsync(null, Arg.Any<CancellationToken>()).Returns([new TypeaheadCandidate("Lunch", 30, null), new TypeaheadCandidate("Fuel", 12, null)]);
        _history.NotesAsync("Mom", Arg.Any<CancellationToken>()).Returns([new TypeaheadCandidate("Gift", 2, null)]);
        _history.AppsAsync(AppSlot.BoughtThrough, null, Arg.Any<CancellationToken>()).Returns([new TypeaheadCandidate("Amazon", 20, null)]);
        _history.AppsAsync(AppSlot.PaidWith, null, Arg.Any<CancellationToken>()).Returns([new TypeaheadCandidate("GPay", 40, null)]);
        _history.AppsAsync(AppSlot.PaidWith, "Mom", Arg.Any<CancellationToken>()).Returns([new TypeaheadCandidate("Cash", 1, null)]);
        _history.AppsAsync(AppSlot.BoughtThrough, "Mom", Arg.Any<CancellationToken>()).Returns(Array.Empty<TypeaheadCandidate>());
        _apps.ListAsync(Arg.Any<CancellationToken>()).Returns([new App { Name = "Paytm" }]);
    }

    [Fact]
    public async Task A_known_contact_gets_its_own_history_first_then_the_most_used()
    {
        var suggest = new FormSuggestions(_history, _apps);
        var pools = await suggest.LoadAsync();

        var lists = await suggest.ForAsync(pools, "Mom", known: true);

        Assert.Equal(["Gift", "Lunch", "Fuel"], TypeaheadRanker.Rank(lists.Notes, null, 3));
        Assert.Equal(["Cash", "GPay", "Paytm"], TypeaheadRanker.Rank(lists.PaidWith, null, 3));
    }

    [Fact]
    public async Task A_new_contact_gets_the_most_used_straight_away_without_a_lookup()
    {
        var suggest = new FormSuggestions(_history, _apps);
        var pools = await suggest.LoadAsync();
        _history.ClearReceivedCalls();

        var lists = await suggest.ForAsync(pools, "Someone new", known: false);

        Assert.Equal(["Lunch", "Fuel"], TypeaheadRanker.Rank(lists.Notes, null, 3));
        Assert.Equal(["Amazon", "Paytm"], TypeaheadRanker.Rank(lists.BoughtThrough, null, 3));
        await _history.DidNotReceive().NotesAsync("Someone new", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_app_never_used_in_that_place_is_still_offered_last()
    {
        var suggest = new FormSuggestions(_history, _apps);
        var pools = await suggest.LoadAsync();

        var lists = await suggest.ForAsync(pools, null, known: false);

        Assert.Equal(["GPay", "Paytm"], TypeaheadRanker.Rank(lists.PaidWith, null, 3));
    }
}
