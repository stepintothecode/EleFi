using System.Text.Json;
using EleFi.Application.Backup;
using EleFi.Application.Containers;
using EleFi.Application.Transactions;
using EleFi.Domain.Containers;
using EleFi.Domain.Filters;
using EleFi.Infrastructure.Tests.Support;

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
        return JsonSerializer.Serialize(file);
    }

    private static BackupFile Read(string json) =>
        JsonSerializer.Deserialize<BackupFile>(json)!;

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
