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
}
