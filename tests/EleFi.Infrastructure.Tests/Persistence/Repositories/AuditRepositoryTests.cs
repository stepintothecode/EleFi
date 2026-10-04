using EleFi.Application.Containers;
using EleFi.Application.Transactions;
using EleFi.Domain.Audit;
using EleFi.Domain.Containers;
using EleFi.Infrastructure.Tests.Support;

namespace EleFi.Infrastructure.Tests.Persistence.Repositories;

/// <summary>
/// The audit trail, which is the answer to "why is this number different from last week".
/// </summary>
/// <remarks>
/// Written by database triggers, so these tests exercise real SQL against a real file.
/// There is no application code to unit test here, which is the point of AU2.
/// </remarks>
public class AuditRepositoryTests
{
    private static readonly DateOnly Today = new(2026, 8, 30);

    [Fact]
    public async Task Creating_a_transaction_records_that_it_was_created()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var saved = await ASpendAsync(f, 450_00);

        var timeline = await f.Audit.TimelineAsync(EditTransactionService.EntityType, saved.Id);

        var created = Assert.Single(timeline);
        Assert.Equal(AuditAction.Created, created.Action);
    }

    [Fact]
    public async Task An_edit_records_which_fields_changed_and_what_they_were()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var saved = await ASpendAsync(f, 450_00);

        var edit = await f.Editing.EditAsync(new EditRequest(
            saved.Id,
            saved.SourcePartyId,
            saved.DestinationPartyId,
            540_00,
            Today,
            Description: "Corrected from the receipt"));

        Assert.True(edit.Succeeded, edit.Error);

        var timeline = await f.Audit.TimelineAsync(EditTransactionService.EntityType, saved.Id);
        var updated = timeline.First(e => e.Action == AuditAction.Updated);
        var changes = updated.ParsedChanges();

        // The trail used to record NULL here, so it could say a transaction was edited but
        // never what changed. That is the difference between a trail and a rumour.
        Assert.Contains(changes, c => c.Field == "SourceAmountMinor" && c.FromText == "45000" && c.ToText == "54000");
        Assert.Contains(changes, c => c.Field == "Description" && c.FromText == "nothing");
    }

    [Fact]
    public async Task AU3_only_fields_that_actually_differed_are_recorded()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var saved = await ASpendAsync(f, 450_00);

        await f.Editing.EditAsync(new EditRequest(
            saved.Id, saved.SourcePartyId, saved.DestinationPartyId, 540_00, Today));

        var timeline = await f.Audit.TimelineAsync(EditTransactionService.EntityType, saved.Id);
        var changes = timeline.First(e => e.Action == AuditAction.Updated).ParsedChanges();

        // Only the amount moved. A trail that lists every column on every edit is one nobody
        // reads, which makes it useless in exactly the moment it is needed.
        Assert.Equal(2, changes.Count(c => c.Field.EndsWith("AmountMinor", StringComparison.Ordinal)));
        Assert.DoesNotContain(changes, c => c.Field == "OccurredOn");
        Assert.DoesNotContain(changes, c => c.Field == "Description");
    }

    [Fact]
    public async Task An_edit_that_changes_nothing_leaves_no_entry()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var saved = await ASpendAsync(f, 450_00);

        await f.Editing.EditAsync(new EditRequest(
            saved.Id, saved.SourcePartyId, saved.DestinationPartyId, 450_00, Today));

        var timeline = await f.Audit.TimelineAsync(EditTransactionService.EntityType, saved.Id);

        // Saving a form without touching it should not add a line to the history. Otherwise
        // the timeline fills with entries that say nothing happened.
        Assert.DoesNotContain(timeline, e => e.Action == AuditAction.Updated);
    }

    [Fact]
    public async Task Attaching_and_removing_a_label_are_both_recorded_by_name()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var saved = await ASpendAsync(f, 450_00);

        var food = (await f.Labels.ListAsync()).First(l => l.Name == "Food");

        await f.Editing.EditAsync(new EditRequest(
            saved.Id, saved.SourcePartyId, saved.DestinationPartyId, 450_00, Today,
            LabelIds: [food.Id]));

        var afterAdd = (await f.Audit.TimelineAsync(EditTransactionService.EntityType, saved.Id))
            .Where(e => e.Action == AuditAction.Updated)
            .SelectMany(e => e.ParsedChanges())
            .ToList();

        // Labels live in a join table, so the Transactions trigger cannot see them. Without
        // the pair on TransactionLabels this edit would leave no trace whatsoever: no tracked
        // column on Transactions changed, so its WHEN clause never fires.
        Assert.Contains(afterAdd, c => c.Field == "Labels" && c.ToText == "Food");

        // By name, not by id. The trail has to still read correctly after the label is
        // renamed or deleted, and "added 0f3c..." explains nothing six months later.
        Assert.DoesNotContain(afterAdd, c => c.Field == "Labels" && c.ToText == food.Id.ToString());

        await f.Editing.EditAsync(new EditRequest(
            saved.Id, saved.SourcePartyId, saved.DestinationPartyId, 450_00, Today));

        var afterRemove = (await f.Audit.TimelineAsync(EditTransactionService.EntityType, saved.Id))
            .Where(e => e.Action == AuditAction.Updated)
            .SelectMany(e => e.ParsedChanges())
            .ToList();

        Assert.Contains(afterRemove, c => c.Field == "Labels" && c.FromText == "Food" && c.ToText == "nothing");
    }

    [Fact]
    public async Task Saving_a_transaction_without_touching_its_labels_adds_no_line()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var saved = await ASpendAsync(f, 450_00);

        var food = (await f.Labels.ListAsync()).First(l => l.Name == "Food");

        await f.Editing.EditAsync(new EditRequest(
            saved.Id, saved.SourcePartyId, saved.DestinationPartyId, 450_00, Today,
            LabelIds: [food.Id]));

        var before = (await f.Audit.TimelineAsync(EditTransactionService.EntityType, saved.Id)).Count;

        // The same labels, again. Clearing and re-adding the collection would delete and
        // reinsert the join rows, and the triggers would write "removed Food, added Food"
        // every time anyone opened the form and pressed save.
        await f.Editing.EditAsync(new EditRequest(
            saved.Id, saved.SourcePartyId, saved.DestinationPartyId, 450_00, Today,
            LabelIds: [food.Id]));

        var after = (await f.Audit.TimelineAsync(EditTransactionService.EntityType, saved.Id)).Count;

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Deleting_a_label_records_the_edit_on_every_transaction_that_carried_it()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var saved = await ASpendAsync(f, 450_00);

        var food = (await f.Labels.ListAsync()).First(l => l.Name == "Food");

        await f.Editing.EditAsync(new EditRequest(
            saved.Id, saved.SourcePartyId, saved.DestinationPartyId, 450_00, Today,
            LabelIds: [food.Id]));

        await f.Labels.DeleteAsync(food.Id);

        var changes = (await f.Audit.TimelineAsync(EditTransactionService.EntityType, saved.Id))
            .Where(e => e.Action == AuditAction.Updated)
            .SelectMany(e => e.ParsedChanges())
            .ToList();

        // The transaction genuinely changed: it used to be labelled Food and now is not.
        // Finding that out later is the entire purpose of the trail.
        Assert.Contains(changes, c => c.Field == "Labels" && c.FromText == "Food" && c.ToText == "nothing");
    }

    [Fact]
    public async Task Deleting_and_restoring_are_both_recorded()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var saved = await ASpendAsync(f, 450_00);

        await f.Editing.DeleteAsync(saved.Id);
        await f.Editing.RestoreAsync(saved.Id);

        var timeline = await f.Audit.TimelineAsync(EditTransactionService.EntityType, saved.Id);

        Assert.Contains(timeline, e => e.Action == AuditAction.Deleted);
        Assert.Contains(timeline, e => e.Action == AuditAction.Restored);

        // AU1: nothing removes entries, including deleting their subject.
        Assert.Contains(timeline, e => e.Action == AuditAction.Created);
    }

    [Fact]
    public async Task T7_an_edit_cannot_move_a_transaction_into_the_future()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var saved = await ASpendAsync(f, 450_00);

        var edit = await f.Editing.EditAsync(new EditRequest(
            saved.Id, saved.SourcePartyId, saved.DestinationPartyId, 450_00, Today.AddDays(1)));

        // Editing is not a back door around the rules capture enforces.
        Assert.False(edit.Succeeded);
        Assert.Contains("future", edit.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<EleFi.Domain.Transactions.Transaction> ASpendAsync(TestDatabase f, long minor)
    {
        var container = await f.ContainerService.CreateAsync(
            new CreateContainerRequest("HDFC", ContainerKind.BankAccount, "INR", 50_000_00, Today.AddYears(-1)));

        var party = await f.Containers.PartyForAsync(container.Container!.Id);
        var shop = await f.Parties.GetOrCreateExternalAsync("Zomato");

        var result = await f.Capture.CaptureAsync(new CaptureRequest(
            party!.Id, shop.Id, minor, "INR", minor, "INR", Today));

        Assert.True(result.Succeeded, result.Error);
        return result.Transaction!;
    }
}
