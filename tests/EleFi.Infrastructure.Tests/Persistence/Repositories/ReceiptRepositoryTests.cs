using EleFi.Application.Containers;
using EleFi.Application.Transactions;
using EleFi.Domain.Containers;
using EleFi.Domain.Receipts;
using EleFi.Infrastructure.Persistence.Repositories;
using EleFi.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Tests.Persistence.Repositories;

/// <summary>Receipts stored with their photos apart, soft-deleted, and in the transaction's history.</summary>
public class ReceiptRepositoryTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public async Task A_receipt_lists_without_its_photo_and_the_photo_and_thumbnail_load_on_their_own()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var receipts = new ReceiptRepository(f.Db, f.Clock);
        var id = await SpendAsync(f);
        var receipt = Receipt(id, 450_00);

        await receipts.AddAsync(receipt, new ReceiptImage { ReceiptId = receipt.Id, Photo = [1, 2, 3], Thumbnail = [7] });

        Assert.Equal([receipt.Id], (await receipts.ForTransactionAsync(id)).Select(r => r.Id));
        Assert.Equal([7], (await receipts.ThumbnailsAsync([receipt.Id]))[receipt.Id]);
        Assert.Equal([1, 2, 3], await receipts.PhotoAsync(receipt.Id));
    }

    [Fact]
    public async Task A_receipt_is_found_by_its_reference_while_it_is_attached()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var receipts = new ReceiptRepository(f.Db, f.Clock);
        var id = await SpendAsync(f);
        var receipt = Receipt(id, 450_00);
        receipt.Reference = "131604324342";
        await receipts.AddAsync(receipt, null);

        Assert.Equal(receipt.Id, (await receipts.FindByReferenceAsync("131604324342"))!.Id);

        await receipts.SoftDeleteAsync(receipt.Id);
        Assert.Null(await receipts.FindByReferenceAsync("131604324342"));
    }

    [Fact]
    public async Task A_removed_receipt_is_kept_but_no_longer_listed()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var receipts = new ReceiptRepository(f.Db, f.Clock);
        var id = await SpendAsync(f);
        var receipt = Receipt(id, 450_00);
        await receipts.AddAsync(receipt, null);

        await receipts.SoftDeleteAsync(receipt.Id);

        Assert.Empty(await receipts.ForTransactionAsync(id));
        Assert.Single(await f.Db.Receipts.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Attaching_settling_and_removing_a_receipt_show_in_the_transactions_history()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var receipts = new ReceiptRepository(f.Db, f.Clock);
        var id = await SpendAsync(f);
        var receipt = Receipt(id, 450_00);

        await receipts.AddAsync(receipt, null);
        receipt.SettledAt = f.Clock.UtcNow;
        await receipts.UpdateAsync(receipt);
        await receipts.SoftDeleteAsync(receipt.Id);

        var changes = (await f.Editing.HistoryAsync(id)).SelectMany(e => e.Changes).ToList();
        Assert.Contains(new HistoryChange("Receipt", "nothing", "Gnani Mart ₹450.00"), changes);
        Assert.Contains(new HistoryChange("Receipt", "Gnani Mart ₹450.00", "kept as it is"), changes);
        Assert.Contains(new HistoryChange("Receipt", "Gnani Mart ₹450.00", "nothing"), changes);
    }

    [Fact]
    public async Task Only_four_digits_of_an_account_fit()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var id = await SpendAsync(f);
        var receipt = Receipt(id, 450_00);
        receipt.AccountLast4 = "12345678";

        await Assert.ThrowsAnyAsync<Exception>(() => new ReceiptRepository(f.Db, f.Clock).AddAsync(receipt, null));
    }

    private static Receipt Receipt(Guid transactionId, long minor) => new()
    {
        TransactionId = transactionId,
        Kind = ReceiptKind.Shop,
        Payee = "Gnani Mart",
        AmountMinor = minor,
        OccurredOn = Today,
    };

    private static async Task<Guid> SpendAsync(TestDatabase f)
    {
        var bank = await f.ContainerService.CreateAsync(new CreateContainerRequest(
            "SBI", ContainerKind.BankAccount, "INR", 10_000_00, Today.AddYears(-1)));
        var bankParty = await f.Containers.PartyForAsync(bank.Container!.Id);
        var shop = await f.Parties.GetOrCreateExternalAsync("Gnani Mart");
        var paid = await f.Capture.CaptureAsync(new CaptureRequest(bankParty!.Id, shop.Id, 450_00, "INR", 450_00, "INR", Today));
        return paid.Transaction!.Id;
    }
}
