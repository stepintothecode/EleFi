using System.Text.Json;
using EleFi.Application.Backup;
using EleFi.Application.Containers;
using EleFi.Application.Transactions;
using EleFi.Domain.Alerts;
using EleFi.Domain.Containers;
using EleFi.Domain.Filters;
using EleFi.Domain.Goals;
using EleFi.Domain.Money;
using EleFi.Domain.Notices;
using EleFi.Domain.Planning;
using EleFi.Infrastructure.Persistence;
using EleFi.Infrastructure.Persistence.Repositories;
using EleFi.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Tests.Persistence;

/// <summary>
/// The local JSON backup, exercised the way a user would: export, lose the device, restore.
/// </summary>
/// <remarks>
/// The round trip is the only assertion that matters. A backup that writes a beautiful file
/// nobody can restore is worth nothing, so every test here ends by reading its own output
/// back into an empty database and checking the numbers the user actually sees.
/// </remarks>
public class LocalBackupTests
{
    private static readonly DateOnly Today = new(2026, 8, 30);

    [Fact]
    public async Task A_backup_restores_into_a_fresh_database_with_the_balances_intact()
    {
        var json = await ExportSampleAsync();

        await using var target = await TestDatabase.CreateAsync(Today);
        var restored = await target.Backup.RestoreAsync(Read(json));

        Assert.Equal(3, restored);

        var balances = await target.Transactions.BalancesAsync();
        var bank = balances.Single(b => b.Name == "HDFC");

        // 50,000 opening, minus 2,000 and 450 spent, minus a 1,000 bill payment.
        Assert.Equal(46_550_00, bank.Balance.Minor);

        // 5,000 owed at the start, 1,000 of it repaid. The sign has to survive the file, or
        // a restored card reads as credit.
        var card = balances.Single(b => b.Name == "Card");
        Assert.Equal(4_000_00, card.AmountOwed.Minor);
        Assert.False(card.IsInCredit);
    }

    [Fact]
    public async Task Restoring_replaces_what_is_there_rather_than_merging_into_it()
    {
        var json = await ExportSampleAsync();

        await using var target = await TestDatabase.CreateAsync(Today);

        // Something already on the device, with a name that would collide.
        await target.ContainerService.CreateAsync(
            new CreateContainerRequest("HDFC", ContainerKind.Cash, "INR", 999_00, Today));

        await target.Backup.RestoreAsync(Read(json));

        var balances = await target.Transactions.BalancesAsync();

        // Two, not three. A merge would leave the old wallet alongside a container of the
        // same name, and no picker could tell them apart.
        Assert.Equal(2, balances.Count);
        Assert.Equal(ContainerKind.BankAccount, balances.Single(b => b.Name == "HDFC").Kind);
    }

    [Fact]
    public async Task Every_label_on_a_transaction_survives_the_round_trip()
    {
        var json = await ExportSampleAsync();

        await using var target = await TestDatabase.CreateAsync(Today);
        await target.Backup.RestoreAsync(Read(json));

        var labels = await target.Labels.ListAsync();
        var food = labels.First(l => l.Name == "Food");
        var shopping = labels.First(l => l.Name == "Shopping");

        var both = await target.Transactions.QueryAsync(new TransactionFilter { LabelIds = [food.Id] });

        // The two-label row, with both still attached. Losing the second one on import would
        // be invisible until a breakdown quietly went missing.
        var row = Assert.Single(both);
        Assert.Equal(2, row.Labels.Count);
        Assert.Contains(row.Labels, l => l.LabelId == shopping.Id);
    }

    [Fact]
    public async Task A_file_from_a_newer_version_is_refused_rather_than_half_imported()
    {
        await using var target = await TestDatabase.CreateAsync(Today);

        var file = new BackupFile { SchemaVersion = BackupFile.CurrentSchemaVersion + 1 };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => target.Backup.RestoreAsync(file));

        Assert.Contains("newer version", error.Message, StringComparison.OrdinalIgnoreCase);

        // And nothing was touched on the way to refusing.
        Assert.NotEmpty(await target.Labels.ListAsync());
    }

    [Fact]
    public async Task Kinds_travel_by_name_so_a_reordered_enum_cannot_reinterpret_them()
    {
        var json = await ExportSampleAsync();

        // The literal text, not a number. If this ever becomes "1", a later insertion into
        // ContainerKind turns every restored bank account into something else.
        Assert.Contains("\"BankAccount\"", json, StringComparison.Ordinal);
        Assert.Contains("\"CreditCard\"", json, StringComparison.Ordinal);
    }

    /// Builds a small but complete ledger and serialises it, exactly as the export button does.
    [Fact]
    public async Task Every_table_every_row_and_every_column_comes_back_exactly()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        await FillEveryTableAsync(f);

        var before = await SnapshotAsync(f);
        var empty = before.Where(t => t.Value.Count == 0).Select(t => t.Key).ToList();
        Assert.True(empty.Count == 0, $"Give these tables a row in FillEveryTableAsync, so the round trip covers them: {string.Join(", ", empty)}");

        // Through text, as the user's file would be.
        var json = JsonSerializer.Serialize(await f.Backup.ExportAsync(), BackupJson.Default.BackupFile);

        await using var fresh = await TestDatabase.CreateAsync(Today);
        await fresh.Backup.RestoreAsync(Read(json));

        var after = await SnapshotAsync(fresh);
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach (var (table, rows) in before)
        {
            Assert.Equal(rows, after[table]);
        }
    }

    [Fact]
    public async Task A_column_this_version_does_not_know_is_refused_and_nothing_is_touched()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        await AddContainerAsync(f, "Kept", ContainerKind.Wallet, 100_00);
        var file = Read(JsonSerializer.Serialize(await f.Backup.ExportAsync(), BackupJson.Default.BackupFile));
        file.Tables!["Containers"][0]["AFieldFromTheFuture"] = 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Backup.RestoreAsync(file));

        Assert.Single(await f.Db.Containers.ToListAsync());
    }

    private static async Task FillEveryTableAsync(TestDatabase f)
    {
        var card = await f.ContainerService.CreateAsync(new CreateContainerRequest(
            "HDFC Card", ContainerKind.CreditCard, "INR", 0, Today.AddYears(-1), AccountNumberLast4: "4417"));
        var bank = await f.ContainerService.CreateAsync(new CreateContainerRequest(
            "SBI", ContainerKind.BankAccount, "INR", 50_000_00, Today.AddYears(-1)));
        Assert.True(card.Succeeded && bank.Succeeded);

        // An alert: a Capture Suggestion link, a Needs Review transaction, an app, a payee.
        await f.Alerts.IngestAsync(
            AlertChannel.PaymentApp, PaymentApps.GPay.Package, "Paid ₹450 to Zomato for Lunch", f.Clock.UtcNow, Currency.Inr);

        // A labelled transaction, and a deleted one, which a backup must keep too.
        var bankParty = await f.Containers.PartyForAsync(bank.Container!.Id);
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");
        var label = await f.Db.Labels.FirstAsync();
        var kept = await f.Capture.CaptureAsync(new CaptureRequest(
            bankParty!.Id, shop.Id, 250_00, "INR", 250_00, "INR", Today, new TimeOnly(9, 30), [label.Id], "Groceries"));
        var gone = await f.Capture.CaptureAsync(new CaptureRequest(bankParty.Id, shop.Id, 99_00, "INR", 99_00, "INR", Today));
        await f.Editing.DeleteAsync(gone.Transaction!.Id);

        // A taught rule, beside the built-in ones.
        f.Db.ParseRules.Add(new ParseRule
        {
            Name = "Taught: VM-AMEXIN",
            SenderPattern = "AMEXIN",
            BodyPattern = @"INR (?<amount>[\d,.]+) spent at (?<who>.+)",
            CreatedAt = f.Clock.UtcNow,
            UpdatedAt = f.Clock.UtcNow,
        });
        await f.Db.SaveChangesAsync();

        // A plan, a notice, and a goal with a contribution.
        await new PlanRepository(f.Db, f.Clock).AddAsync(new Plan
        {
            Title = "SIP", AmountMinor = 5_000_00, DueOn = Today, CreatedAt = f.Clock.UtcNow, UpdatedAt = f.Clock.UtcNow,
        });
        await new NoticeRepository(f.Db, f.Clock).UpsertAsync(new Notice
        {
            Title = "₹450 to Zomato", Body = "Recorded, needs review.", Route = "transactions/1",
            CreatedAt = f.Clock.UtcNow, UpdatedAt = f.Clock.UtcNow,
        });
        var goals = new GoalRepository(f.Db, f.Clock);
        var ring = new Goal
        {
            Name = "Ring", TargetAmountMinor = 3_00_000_00, StartDate = Today, TargetDate = Today.AddYears(2),
            FundingMode = FundingMode.TracksContributions, OpeningAllocationMinor = 20_000_00,
            CreatedAt = f.Clock.UtcNow, UpdatedAt = f.Clock.UtcNow,
        };
        ring.Containers.Add(new GoalContainer { GoalId = ring.Id, ContainerId = bank.Container.Id });
        await goals.AddAsync(ring);
        await goals.SetGoalAsync(kept.Transaction!.Id, ring.Id);

        f.Db.ChangeTracker.Clear();
    }

    // Every table, every row in order, every column, as text: what "nothing was lost" means.
    private static async Task<Dictionary<string, List<string>>> SnapshotAsync(TestDatabase f)
    {
        var snapshot = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        await f.Db.Database.OpenConnectionAsync();
        try
        {
            foreach (var table in BackupTables.Of(f.Db.Model))
            {
                using var command = f.Db.Database.GetDbConnection().CreateCommand();
                command.CommandText = $"SELECT * FROM \"{table.Name}\" ORDER BY rowid";
                using var reader = await command.ExecuteReaderAsync();
                var rows = new List<string>();
                while (await reader.ReadAsync())
                {
                    rows.Add(string.Join(" | ", Enumerable.Range(0, reader.FieldCount)
                        .Select(i => $"{reader.GetName(i)}={(reader.IsDBNull(i) ? "null" : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture))}")));
                }

                snapshot[table.Name] = rows;
            }
        }
        finally
        {
            await f.Db.Database.CloseConnectionAsync();
        }

        return snapshot;
    }

    private static async Task<string> ExportSampleAsync()
    {
        await using var source = await TestDatabase.CreateAsync(Today);

        var bank = await AddContainerAsync(source, "HDFC", ContainerKind.BankAccount, 50_000_00);
        var card = await AddContainerAsync(source, "Card", ContainerKind.CreditCard, -5_000_00);
        var shop = await source.Parties.GetOrCreateExternalAsync("Big Bazaar");

        var labels = await source.Labels.ListAsync();
        var food = labels.First(l => l.Name == "Food");
        var shopping = labels.First(l => l.Name == "Shopping");

        await source.Capture.CaptureAsync(new CaptureRequest(
            bank, shop.Id, 2_000_00, "INR", 2_000_00, "INR", Today,
            LabelIds: [food.Id, shopping.Id]));

        await source.Capture.CaptureAsync(new CaptureRequest(
            bank, shop.Id, 450_00, "INR", 450_00, "INR", Today));

        await source.Capture.CaptureAsync(new CaptureRequest(
            bank, card, 1_000_00, "INR", 1_000_00, "INR", Today));

        var file = await source.Backup.ExportAsync();
        return JsonSerializer.Serialize(file, BackupJson.Default.BackupFile);
    }

    private static BackupFile Read(string json) =>
        JsonSerializer.Deserialize(json, BackupJson.Default.BackupFile)!;

    private static async Task<Guid> AddContainerAsync(
        TestDatabase f, string name, ContainerKind kind, long openingMinor)
    {
        var result = await f.ContainerService.CreateAsync(
            new CreateContainerRequest(name, kind, "INR", openingMinor, Today.AddYears(-1)));

        Assert.True(result.Succeeded, result.Error);

        var party = await f.Containers.PartyForAsync(result.Container!.Id);
        return party!.Id;
    }
}
