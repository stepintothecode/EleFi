using EleFi.Application.Abstractions;
using EleFi.Application.Contacts;
using EleFi.Application.Tests.Support;
using EleFi.Domain.Filters;
using EleFi.Domain.Parties;
using NSubstitute;

namespace EleFi.Application.Tests.Contacts;

/// <summary>
/// Contacts: the checks before anything is written, and how the pieces are put together. The
/// moving of rows itself is tested against a database, in ContactRepositoryTests.
/// </summary>
public class ContactServiceTests
{
    private readonly IContactRepository _contacts = Substitute.For<IContactRepository>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly MovableClock _clock = new();
    private readonly Party _rahul = new() { Kind = PartyKind.External, Name = "Rahul" };
    private readonly Party _rahulK = new() { Kind = PartyKind.External, Name = "Rahul Kumar" };

    public ContactServiceTests()
    {
        _contacts.FindAsync(_rahul.Id, Arg.Any<CancellationToken>()).Returns(_rahul);
        _contacts.FindAsync(_rahulK.Id, Arg.Any<CancellationToken>()).Returns(_rahulK);
        _contacts.LinksAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns([]);
        _contacts.DetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    [Fact]
    public async Task P4_a_new_contact_cannot_take_a_name_already_used()
    {
        _contacts.NameTakenAsync("rahul", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await Service().CreateAsync("rahul");

        Assert.False(result.Succeeded);
        Assert.Contains("already", result.Error, StringComparison.Ordinal);
        await _contacts.DidNotReceive().AddAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_blank_name_is_refused()
    {
        Assert.False((await Service().CreateAsync("   ")).Succeeded);
    }

    [Fact]
    public async Task CT4_renaming_keeps_the_old_name_as_an_alias_when_asked()
    {
        var result = await Service().RenameAsync(_rahul.Id, "Rahul Sharma", keepOldName: true);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Rahul Sharma", _rahul.Name);
        await _contacts.Received(1).AddAliasAsync(_rahul.Id, "Rahul", Arg.Any<CancellationToken>());
        await _contacts.Received(1).UpdateAsync(_rahul, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renaming_without_keeping_the_old_name_adds_no_alias()
    {
        await Service().RenameAsync(_rahul.Id, "Rahul Sharma", keepOldName: false);

        await _contacts.DidNotReceive().AddAliasAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_change_of_case_only_is_a_rename_and_needs_no_alias()
    {
        await Service().RenameAsync(_rahul.Id, "RAHUL", keepOldName: true);

        Assert.Equal("RAHUL", _rahul.Name);
        await _contacts.DidNotReceive().AddAliasAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renaming_onto_another_contacts_name_is_refused_and_suggests_merging()
    {
        _contacts.NameTakenAsync("Rahul Kumar", _rahul.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await Service().RenameAsync(_rahul.Id, "Rahul Kumar", keepOldName: true);

        Assert.False(result.Succeeded);
        Assert.Contains("Merge", result.Error, StringComparison.Ordinal);
        Assert.Equal("Rahul", _rahul.Name);
    }

    [Fact]
    public async Task CT2_a_detail_that_is_not_what_it_says_is_refused_and_nothing_is_kept()
    {
        var added = await Service().AddDetailAsync(_rahul.Id, ContactDetailKind.Email, "rahul at gmail", null);

        Assert.NotNull(added.Error);
        Assert.Null(added.Detail);
        await _contacts.DidNotReceive().AddDetailAsync(Arg.Any<ContactDetail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_good_detail_is_kept_against_the_contact()
    {
        var added = await Service().AddDetailAsync(_rahul.Id, ContactDetailKind.Phone, "+91 98765 43210", "Mobile");

        Assert.Null(added.Error);
        Assert.Equal("+91 98765 43210", added.Detail!.Value);
        await _contacts.Received(1).AddDetailAsync(
            Arg.Is<ContactDetail>(d => d.PartyId == _rahul.Id && d.Value == "+91 98765 43210" && d.Label == "Mobile"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CT3_a_contact_cannot_be_linked_to_itself_or_to_the_same_contact_twice()
    {
        var service = Service();

        Assert.NotNull(await service.LinkAsync(_rahul.Id, _rahul.Id, ContactRelation.SameAs));

        // The repository returns a contact's links at either end.
        _contacts.LinksAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([ContactLink.Between(_rahul.Id, _rahulK.Id, ContactRelation.AnotherAccountOf, _clock.UtcNow)]);

        Assert.NotNull(await service.LinkAsync(_rahulK.Id, _rahul.Id, ContactRelation.SameAs));
        await _contacts.DidNotReceive().AddLinkAsync(Arg.Any<ContactLink>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Linking_two_contacts_stores_one_link()
    {
        Assert.Null(await Service().LinkAsync(_rahul.Id, _rahulK.Id, ContactRelation.AnotherAccountOf));

        await _contacts.Received(1).AddLinkAsync(
            Arg.Is<ContactLink>(l => l.Joins(_rahul.Id, _rahulK.Id) && l.Relation == ContactRelation.AnotherAccountOf),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CT4_merging_a_contact_into_itself_or_into_something_that_is_not_a_contact_is_refused()
    {
        var service = Service();

        Assert.NotNull((await service.MergeAsync(_rahul.Id, _rahul.Id)).Error);
        Assert.NotNull((await service.MergeAsync(_rahul.Id, Guid.CreateVersion7())).Error);
        await _contacts.DidNotReceive().MergeAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Merging_hands_back_what_moved_so_it_can_be_undone()
    {
        var moved = new ContactMerge(_rahul.Id, _rahulK.Id, [], [], [], [], [], [], null, null, 0);
        _contacts.MergeAsync(_rahul.Id, _rahulK.Id, Arg.Any<CancellationToken>()).Returns(moved);
        var service = Service();

        var result = await service.MergeAsync(_rahul.Id, _rahulK.Id);
        await service.UndoMergeAsync(result.Merge!);

        Assert.Null(result.Error);
        await _contacts.Received(1).UndoMergeAsync(moved, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Possible_duplicates_leave_out_pairs_the_user_already_answered()
    {
        var swiggy = Summary("Swiggy");
        var swiggyLtd = Summary("SWIGGY LTD");
        var amazon = Summary("Amazon");
        var amazonPay = Summary("Amazon Pay");
        _contacts.ListAsync(Arg.Any<CancellationToken>()).Returns([swiggy, swiggyLtd, amazon, amazonPay]);
        _contacts.LinksAsync(null, Arg.Any<CancellationToken>())
            .Returns([ContactLink.Between(amazon.Id, amazonPay.Id, ContactRelation.NotTheSame, _clock.UtcNow)]);

        var found = await Service().DuplicatesAsync();

        var only = Assert.Single(found);
        Assert.Equal(["Swiggy", "SWIGGY LTD"], new[] { only.First.Name, only.Second.Name }.Order());
        Assert.Equal(DuplicateReason.SameName, only.Reason);
    }

    [Fact]
    public async Task A_contacts_page_counts_its_transactions_and_its_money_in_and_out()
    {
        _transactions.CountAsync(Arg.Is<TransactionFilter>(f => f.PartyIds.SequenceEqual(new[] { _rahul.Id })), Arg.Any<CancellationToken>())
            .Returns(3);
        _transactions.TotalsAsync(Arg.Is<TransactionFilter>(f => f.PartyIds.SequenceEqual(new[] { _rahul.Id })), Arg.Any<CancellationToken>())
            .Returns(new FlowTotals(500_00, 1_200_00));
        _contacts.AliasesAsync(_rahul.Id, Arg.Any<CancellationToken>()).Returns(["RAHUL K"]);

        var profile = await Service().ProfileAsync(_rahul.Id);

        Assert.Equal(3, profile!.TransactionCount);
        Assert.Equal(1_200_00, profile.Totals.OutMinor);
        Assert.Equal(500_00, profile.Totals.InMinor);
        Assert.Equal(["RAHUL K"], profile.Aliases);
    }

    [Fact]
    public async Task Linked_contacts_are_named_on_the_page_but_not_the_same_ones_are_not()
    {
        var stranger = new Party { Kind = PartyKind.External, Name = "Rahul Verma" };
        _contacts.FindAsync(stranger.Id, Arg.Any<CancellationToken>()).Returns(stranger);
        _contacts.LinksAsync(_rahul.Id, Arg.Any<CancellationToken>()).Returns(
        [
            ContactLink.Between(_rahul.Id, _rahulK.Id, ContactRelation.AnotherAccountOf, _clock.UtcNow),
            ContactLink.Between(_rahul.Id, stranger.Id, ContactRelation.NotTheSame, _clock.UtcNow),
        ]);

        var profile = await Service().ProfileAsync(_rahul.Id);

        var linked = Assert.Single(profile!.Linked);
        Assert.Equal("Rahul Kumar", linked.Name);
        Assert.Equal(ContactRelation.AnotherAccountOf, linked.Relation);
    }

    [Fact]
    public async Task Showing_transactions_with_linked_contacts_includes_them_and_only_them()
    {
        var stranger = Guid.CreateVersion7();
        _contacts.LinksAsync(_rahul.Id, Arg.Any<CancellationToken>()).Returns(
        [
            ContactLink.Between(_rahul.Id, _rahulK.Id, ContactRelation.SameAs, _clock.UtcNow),
            ContactLink.Between(_rahul.Id, stranger, ContactRelation.NotTheSame, _clock.UtcNow),
        ]);
        var service = Service();

        Assert.Equal([_rahul.Id], (await service.FilterForAsync(_rahul.Id, includeLinked: false)).PartyIds);
        Assert.Equal(new[] { _rahul.Id, _rahulK.Id }.Order(), (await service.FilterForAsync(_rahul.Id, includeLinked: true)).PartyIds.Order());
    }

    [Fact]
    public async Task A_phone_contact_with_a_new_name_becomes_a_contact_with_its_number()
    {
        var added = new Party { Kind = PartyKind.External, Name = "Priya" };
        _contacts.AddAsync("Priya", Arg.Any<CancellationToken>()).Returns(added);
        _contacts.FindAsync(added.Id, Arg.Any<CancellationToken>()).Returns(added);

        var result = await Service().CreateFromPhoneAsync(new PickedContact("Priya", "98765 43210", "lookup-1"));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("lookup-1", added.PhoneContactKey);
        await _contacts.Received(1).AddDetailAsync(
            Arg.Is<ContactDetail>(d => d.PartyId == added.Id && d.Kind == ContactDetailKind.Phone),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Linking_a_phone_contact_adds_its_number_once()
    {
        _contacts.DetailsAsync(_rahul.Id, Arg.Any<CancellationToken>())
            .Returns([ContactDetail.Create(_rahul.Id, ContactDetailKind.Phone, "98765 43210", null, _clock.UtcNow)]);

        await Service().LinkPhoneContactAsync(_rahul.Id, new PickedContact("Rahul K", "98765 43210", "lookup-2"));

        Assert.Equal("lookup-2", _rahul.PhoneContactKey);
        await _contacts.DidNotReceive().AddDetailAsync(Arg.Any<ContactDetail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unlinking_a_pair_removes_the_link_between_them_for_undo()
    {
        var link = ContactLink.Between(_rahul.Id, _rahulK.Id, ContactRelation.SameAs, _clock.UtcNow);
        _contacts.LinksAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns([link]);

        await Service().UnlinkBetweenAsync(_rahulK.Id, _rahul.Id);

        await _contacts.Received(1).RemoveLinkAsync(link.Id, Arg.Any<CancellationToken>());
    }
    private ContactService Service() => new(_contacts, _transactions, _clock);

    private static ContactSummary Summary(string name) => new(Guid.CreateVersion7(), name, 1, null, 0, false);
}
