using EleFi.Application.Containers;
using EleFi.Application.Transactions;
using EleFi.Domain.Audit;
using EleFi.Domain.Containers;
using EleFi.Domain.Filters;
using EleFi.Domain.Parties;
using EleFi.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Tests.Persistence.Repositories;

/// <summary>
/// Contacts against a real database: the list, details and links, aliases, and a merge that
/// moves everything and can be undone exactly.
/// </summary>
public class ContactRepositoryTests
{
    private static readonly DateOnly Today = new(2026, 10, 11);

    [Fact]
    public async Task The_list_has_every_contact_with_its_count_and_never_a_container_or_an_alias()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var bank = await BankAsync(f);
        var rahul = await f.Parties.GetOrCreateExternalAsync("Rahul");
        await SpendAsync(f, bank, rahul.Id);
        await SpendAsync(f, bank, rahul.Id);
        await f.Contacts.AddAsync("Priya");
        await f.Contacts.AddAliasAsync(rahul.Id, "Rahul K");

        var list = await f.Contacts.ListAsync();

        Assert.Equal(["Priya", "Rahul"], list.Select(c => c.Name));
        Assert.Equal(2, list.Single(c => c.Name == "Rahul").TransactionCount);
    }

    [Fact]
    public async Task Details_and_links_are_kept_and_removing_them_is_soft()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var rahul = await f.Contacts.AddAsync("Rahul");
        var priya = await f.Contacts.AddAsync("Priya");
        var phone = ContactDetail.Create(rahul.Id, ContactDetailKind.Phone, "98765 43210", "Mobile", f.Clock.UtcNow);
        await f.Contacts.AddDetailAsync(phone);
        var link = ContactLink.Between(rahul.Id, priya.Id, ContactRelation.SameAs, f.Clock.UtcNow);
        await f.Contacts.AddLinkAsync(link);

        Assert.Single(await f.Contacts.DetailsAsync(rahul.Id));
        Assert.Single(await f.Contacts.LinksAsync(priya.Id));

        await f.Contacts.RemoveDetailAsync(phone.Id);
        await f.Contacts.RemoveLinkAsync(link.Id);

        Assert.Empty(await f.Contacts.DetailsAsync(rahul.Id));
        Assert.Empty(await f.Contacts.LinksAsync(null));
        Assert.Single(await f.Db.ContactDetails.IgnoreQueryFilters().ToListAsync());
        Assert.Single(await f.Db.ContactLinks.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task CT3_the_database_refuses_a_second_live_link_between_the_same_two()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var rahul = await f.Contacts.AddAsync("Rahul");
        var priya = await f.Contacts.AddAsync("Priya");
        await f.Contacts.AddLinkAsync(ContactLink.Between(rahul.Id, priya.Id, ContactRelation.SameAs, f.Clock.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            f.Contacts.AddLinkAsync(ContactLink.Between(priya.Id, rahul.Id, ContactRelation.AnotherAccountOf, f.Clock.UtcNow)));
    }

    [Fact]
    public async Task CT4_an_alias_is_a_taken_name_except_for_the_contact_it_belongs_to()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var rahul = await f.Contacts.AddAsync("Rahul Sharma");
        await f.Contacts.AddAliasAsync(rahul.Id, "Rahul");

        Assert.True(await f.Contacts.NameTakenAsync("rahul"));
        Assert.False(await f.Contacts.NameTakenAsync("rahul", rahul.Id));
        Assert.True(await f.Contacts.NameTakenAsync("RAHUL SHARMA"));
        Assert.Equal(["Rahul"], await f.Contacts.AliasesAsync(rahul.Id));
    }

    [Fact]
    public async Task CT4_capturing_with_an_alias_lands_on_the_contact_it_points_at()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var rahul = await f.Contacts.AddAsync("Rahul Sharma");
        await f.Contacts.AddAliasAsync(rahul.Id, "RAHUL S");

        // An SMS keeps writing the bank's spelling; the user renamed the contact long ago.
        var found = await f.Parties.GetOrCreateExternalAsync("rahul s");

        Assert.Equal(rahul.Id, found.Id);
        Assert.Single(await f.Parties.ListExternalAsync());
    }

    [Fact]
    public async Task CT4_a_merge_moves_transactions_details_and_links_and_leaves_the_old_name_as_an_alias()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var bank = await BankAsync(f);
        var (rahul, rahulK, priya) = await ThreeAsync(f, bank);
        var before = await f.Transactions.BalancesAsync();

        await f.Contacts.MergeAsync(rahul.Id, rahulK.Id);
        f.Db.ChangeTracker.Clear();

        // Every transaction is now Rahul's, and not a rupee moved anywhere.
        Assert.Equal(3, await f.Transactions.CountAsync(new TransactionFilter { PartyIds = [rahul.Id] }));
        Assert.Equal(0, await f.Transactions.CountAsync(new TransactionFilter { PartyIds = [rahulK.Id] }));
        Assert.Equal(before.Select(b => b.Balance), (await f.Transactions.BalancesAsync()).Select(b => b.Balance));

        Assert.Single(await f.Contacts.DetailsAsync(rahul.Id));
        Assert.Contains(await f.Contacts.LinksAsync(rahul.Id), l => l.Joins(rahul.Id, priya.Id));
        Assert.Equal(["Rahul K"], await f.Contacts.AliasesAsync(rahul.Id));
        Assert.DoesNotContain(await f.Contacts.ListAsync(), c => c.Id == rahulK.Id);
        Assert.Equal("Lent him the drill\n\nPays back by UPI", (await f.Contacts.FindAsync(rahul.Id))!.Notes);

        // An alert still writing the old name lands on the contact that stayed.
        Assert.Equal(rahul.Id, (await f.Parties.GetOrCreateExternalAsync("RAHUL K")).Id);
    }

    [Fact]
    public async Task CT4_a_link_between_the_two_merged_contacts_is_retired_not_turned_into_a_self_link()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var rahul = await f.Contacts.AddAsync("Rahul");
        var rahulK = await f.Contacts.AddAsync("Rahul K");
        await f.Contacts.AddLinkAsync(ContactLink.Between(rahul.Id, rahulK.Id, ContactRelation.SameAs, f.Clock.UtcNow));

        await f.Contacts.MergeAsync(rahul.Id, rahulK.Id);

        Assert.Empty(await f.Contacts.LinksAsync(rahul.Id));
    }

    [Fact]
    public async Task Each_moved_transactions_history_says_who_it_moved_from_and_to()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var bank = await BankAsync(f);
        var (rahul, rahulK, _) = await ThreeAsync(f, bank);
        var moved = (await f.Transactions.QueryAsync(new TransactionFilter { PartyIds = [rahulK.Id] })).Single();

        await f.Contacts.MergeAsync(rahul.Id, rahulK.Id);

        var history = await f.Editing.HistoryAsync(moved.Id);
        Assert.Contains(history.SelectMany(h => h.Changes), c => c is { Field: "Paid to", From: "Rahul K", To: "Rahul" });
    }

    [Fact]
    public async Task Undoing_a_merge_puts_everything_back_exactly()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var bank = await BankAsync(f);
        var (rahul, rahulK, priya) = await ThreeAsync(f, bank);
        f.Db.ChangeTracker.Clear();
        var usage = (await f.Contacts.FindAsync(rahul.Id))!.UsageCount;

        var merge = await f.Contacts.MergeAsync(rahul.Id, rahulK.Id);
        await f.Contacts.UndoMergeAsync(merge);
        f.Db.ChangeTracker.Clear();

        Assert.Equal(2, await f.Transactions.CountAsync(new TransactionFilter { PartyIds = [rahul.Id] }));
        Assert.Equal(1, await f.Transactions.CountAsync(new TransactionFilter { PartyIds = [rahulK.Id] }));
        Assert.Single(await f.Contacts.DetailsAsync(rahulK.Id));
        Assert.Empty(await f.Contacts.DetailsAsync(rahul.Id));
        Assert.Contains(await f.Contacts.LinksAsync(rahulK.Id), l => l.Joins(rahulK.Id, priya.Id));
        Assert.Empty(await f.Contacts.AliasesAsync(rahul.Id));
        Assert.Equal(["Priya", "Rahul", "Rahul K"], (await f.Contacts.ListAsync()).Select(c => c.Name));

        var kept = (await f.Contacts.FindAsync(rahul.Id))!;
        Assert.Equal(usage, kept.UsageCount);
        Assert.Equal("Lent him the drill", kept.Notes);
    }

    private static async Task<(Party Rahul, Party RahulK, Party Priya)> ThreeAsync(TestDatabase f, Guid bank)
    {
        var rahul = await f.Parties.GetOrCreateExternalAsync("Rahul");
        var rahulK = await f.Parties.GetOrCreateExternalAsync("Rahul K");
        var priya = await f.Contacts.AddAsync("Priya");
        await SpendAsync(f, bank, rahul.Id);
        await SpendAsync(f, bank, rahul.Id);
        await SpendAsync(f, bank, rahulK.Id);

        rahul.Notes = "Lent him the drill";
        rahulK.Notes = "Pays back by UPI";
        await f.Db.SaveChangesAsync();

        await f.Contacts.AddDetailAsync(ContactDetail.Create(rahulK.Id, ContactDetailKind.Phone, "98765 43210", null, f.Clock.UtcNow));
        await f.Contacts.AddLinkAsync(ContactLink.Between(rahulK.Id, priya.Id, ContactRelation.AnotherAccountOf, f.Clock.UtcNow));
        return (rahul, rahulK, priya);
    }

    private static async Task<Guid> BankAsync(TestDatabase f)
    {
        var result = await f.ContainerService.CreateAsync(
            new CreateContainerRequest("HDFC", ContainerKind.BankAccount, "INR", 50_000_00, Today.AddYears(-1)));
        Assert.True(result.Succeeded, result.Error);
        return (await f.Containers.PartyForAsync(result.Container!.Id))!.Id;
    }

    private static async Task SpendAsync(TestDatabase f, Guid bank, Guid payee)
    {
        var result = await f.Capture.CaptureAsync(new CaptureRequest(bank, payee, 100_00, "INR", 100_00, "INR", Today));
        Assert.True(result.Succeeded, result.Error);
    }
}
