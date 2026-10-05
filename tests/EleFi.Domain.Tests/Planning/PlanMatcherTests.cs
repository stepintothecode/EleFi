using EleFi.Domain.Parties;
using EleFi.Domain.Planning;
using EleFi.Domain.Transactions;

namespace EleFi.Domain.Tests.Planning;

/// <summary>Finding the transaction that already records a plan, so nothing is recorded twice.</summary>
public class PlanMatcherTests
{
    private static readonly DateOnly Due = new(2026, 10, 5);
    private static readonly Guid Sbi = Guid.NewGuid();
    private static readonly Guid Fund = Guid.NewGuid();

    private static readonly Plan Sip = new()
    {
        Title = "SIP",
        AmountMinor = 5_000_00,
        Direction = TransactionKind.SelfTransfer,
        ContainerId = Sbi,
        DestinationContainerId = Fund,
        DueOn = Due,
    };

    [Fact]
    public void The_banks_debit_for_the_sip_matches_even_though_it_names_the_fund_house()
    {
        Assert.True(PlanMatcher.Matches(Sip, Debit(Sbi, 5_000_00, Due.AddDays(1))));
    }

    [Fact]
    public void A_transfer_into_the_fund_container_matches_too()
    {
        Assert.True(PlanMatcher.Matches(Sip, Transfer(Sbi, Fund, 5_000_00, Due)));
    }

    [Theory]
    [InlineData(4_999_99, 0)]
    [InlineData(5_000_00, 5)]
    [InlineData(5_000_00, -5)]
    public void A_different_amount_or_a_date_too_far_away_does_not_match(long minor, int days)
    {
        Assert.False(PlanMatcher.Matches(Sip, Debit(Sbi, minor, Due.AddDays(days))));
    }

    [Fact]
    public void A_debit_from_another_container_does_not_match()
    {
        Assert.False(PlanMatcher.Matches(Sip, Debit(Guid.NewGuid(), 5_000_00, Due)));
    }

    [Fact]
    public void Money_arriving_never_matches_a_plan_for_money_leaving()
    {
        var rent = new Plan { AmountMinor = 100, Direction = TransactionKind.Debit, DueOn = Due };

        Assert.False(PlanMatcher.Matches(rent, Credit(Sbi, 100, Due)));
    }

    [Fact]
    public void A_plan_without_an_amount_never_matches_automatically()
    {
        Assert.False(PlanMatcher.Matches(new Plan { DueOn = Due }, Debit(Sbi, 100, Due)));
    }

    [Fact]
    public void Candidates_skip_transactions_another_plan_has_claimed_and_put_the_nearest_first()
    {
        var far = Debit(Sbi, 5_000_00, Due.AddDays(3));
        var near = Debit(Sbi, 5_000_00, Due);
        var claimed = Debit(Sbi, 5_000_00, Due.AddDays(1));

        var candidates = PlanMatcher.Candidates(Sip, [far, near, claimed], new HashSet<Guid> { claimed.Id });

        Assert.Equal([near.Id, far.Id], candidates.Select(c => c.Id));
    }

    private static Party Mine(Guid container) => new() { Kind = PartyKind.Container, ContainerId = container };

    private static Party Outside() => new() { Kind = PartyKind.External, Name = "ICICI Prudential MF" };

    private static Transaction Debit(Guid from, long minor, DateOnly on) => Make(Mine(from), Outside(), minor, on);

    private static Transaction Credit(Guid into, long minor, DateOnly on) => Make(Outside(), Mine(into), minor, on);

    private static Transaction Transfer(Guid from, Guid into, long minor, DateOnly on) => Make(Mine(from), Mine(into), minor, on);

    private static Transaction Make(Party source, Party destination, long minor, DateOnly on) => new()
    {
        SourceParty = source,
        SourcePartyId = source.Id,
        DestinationParty = destination,
        DestinationPartyId = destination.Id,
        SourceAmountMinor = minor,
        DestinationAmountMinor = minor,
        OccurredOn = on,
    };
}
