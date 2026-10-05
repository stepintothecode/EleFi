using EleFi.Application.Containers;
using EleFi.Application.Transactions;
using EleFi.Domain.Containers;
using EleFi.Domain.Filters;
using EleFi.Domain.Transactions;
using EleFi.Infrastructure.Tests.Support;

namespace EleFi.Infrastructure.Tests.Persistence.Repositories;

/// <summary>
/// The queries the whole app stands on: balances, filters, and the one query the export
/// shares with the list.
/// </summary>
public class TransactionRepositoryTests
{
    private static readonly DateOnly Today = new(2026, 8, 30);

    [Fact]
    public async Task D2_balance_is_derived_from_opening_balance_plus_movements()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (bank, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 10_000_00);
        var shop = await f.Parties.GetOrCreateExternalAsync("Big Bazaar");

        await SpendAsync(f, bankParty, shop, 450_00);
        await SpendAsync(f, bankParty, shop, 1_050_00);

        var balances = await f.Transactions.BalancesAsync();
        var balance = balances.Single(b => b.ContainerId == bank.Id);

        // 10,000.00 - 450.00 - 1,050.00 = 8,500.00
        Assert.Equal(8_500_00, balance.Balance.Minor);
    }

    [Fact]
    public async Task INV_CC_card_spending_then_paying_the_bill_leaves_net_worth_unchanged()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 50_000_00);
        var (_, cardParty) = await AddContainerAsync(f, "HDFC Card", ContainerKind.CreditCard, 0);
        var shop = await f.Parties.GetOrCreateExternalAsync("Amazon");

        var before = await f.ContainerService.NetWorthAsync(EleFi.Domain.Money.Currency.Inr);

        // Spending on the card is a Debit with the card as source: it increases what is
        // owed and touches no bank balance.
        await SpendAsync(f, cardParty, shop, 2_000_00);

        var afterSpend = await f.ContainerService.NetWorthAsync(EleFi.Domain.Money.Currency.Inr);
        Assert.Equal(before.Total.Minor - 2_000_00, afterSpend.Total.Minor);
        Assert.Equal(2_000_00, afterSpend.Owed.Minor);

        // Paying the bill is a Self Transfer bank -> card. One asset down, one liability
        // down, so net worth must not move.
        await TransferAsync(f, bankParty, cardParty, 2_000_00);

        var afterPayment = await f.ContainerService.NetWorthAsync(EleFi.Domain.Money.Currency.Inr);

        Assert.Equal(afterSpend.Total.Minor, afterPayment.Total.Minor);
        Assert.Equal(0, afterPayment.Owed.Minor);
        Assert.Equal(48_000_00, afterPayment.Liquid.Minor);
    }

    [Fact]
    public async Task Repaying_a_card_reduces_what_is_owed_rather_than_increasing_it()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 50_000_00);

        // Entered the way the UI asks: "how much do you owe", stored negative.
        var (card, cardParty) = await AddContainerAsync(f, "Card", ContainerKind.CreditCard, -5_000_00);

        var owedBefore = (await f.Transactions.BalancesAsync()).Single(b => b.ContainerId == card.Id);
        Assert.Equal(5_000_00, owedBefore.AmountOwed.Minor);

        // The repayment: a Self Transfer from the bank to the card.
        await TransferAsync(f, bankParty, cardParty, 2_000_00);

        var owedAfter = (await f.Transactions.BalancesAsync()).Single(b => b.ContainerId == card.Id);

        // Used to read 7,000 owed, because AmountOwed took Abs() of the balance and the
        // sign that distinguished debt from credit was thrown away.
        Assert.Equal(3_000_00, owedAfter.AmountOwed.Minor);
        Assert.False(owedAfter.IsInCredit);
    }

    [Fact]
    public async Task Overpaying_a_card_reads_as_credit_not_as_more_debt()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 50_000_00);
        var (card, cardParty) = await AddContainerAsync(f, "Card", ContainerKind.CreditCard, 0);

        // Paying onto a card with nothing on it. This is the exact sequence that reported
        // "2,000 owed" when the issuer in fact owed the user.
        await TransferAsync(f, bankParty, cardParty, 2_000_00);

        var balance = (await f.Transactions.BalancesAsync()).Single(b => b.ContainerId == card.Id);

        Assert.True(balance.IsInCredit);
        Assert.Equal(-2_000_00, balance.AmountOwed.Minor);

        // And net worth is unchanged: the money moved from a bank to a card, both the
        // user's own.
        var net = await f.ContainerService.NetWorthAsync(EleFi.Domain.Money.Currency.Inr);
        Assert.Equal(50_000_00, net.Total.Minor);
    }

    [Fact]
    public async Task D1_self_transfers_never_appear_in_spend_by_label()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 50_000_00);
        var (_, cardParty) = await AddContainerAsync(f, "Card", ContainerKind.CreditCard, 0);
        var shop = await f.Parties.GetOrCreateExternalAsync("Zomato");

        await SpendAsync(f, cardParty, shop, 500_00);

        // The bill payment. If this leaked into spend, card spending would be counted twice.
        await TransferAsync(f, bankParty, cardParty, 500_00);

        var spend = await f.Transactions.SpendByLabelAsync(new TransactionFilter { From = Today.AddDays(-30), To = Today });

        Assert.Equal(500_00, spend.TotalMinor);
    }

    [Fact]
    public async Task A_transaction_with_two_labels_counts_in_full_under_each()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 50_000_00);
        var shop = await f.Parties.GetOrCreateExternalAsync("Big Bazaar");

        var labels = await f.Labels.ListAsync();
        var food = labels.First(l => l.Name == "Food");
        var shopping = labels.First(l => l.Name == "Shopping");

        await f.Capture.CaptureAsync(new CaptureRequest(
            bankParty, shop.Id, 2_000_00, "INR", 2_000_00, "INR", Today,
            LabelIds: [food.Id, shopping.Id]));

        var spend = await f.Transactions.SpendByLabelAsync(new TransactionFilter { From = Today.AddDays(-30), To = Today });

        // This is the deliberate consequence of ADR-0013: the per-label figures overlap.
        Assert.Equal(2_000_00, spend.ByLabel.Single(s => s.LabelId == food.Id).AmountMinor);
        Assert.Equal(2_000_00, spend.ByLabel.Single(s => s.LabelId == shopping.Id).AmountMinor);

        // And this is why the total is counted separately rather than summed from them.
        // Summing would say 4,000 against 2,000 of real money.
        Assert.Equal(2_000_00, spend.TotalMinor);
        Assert.Equal(4_000_00, spend.ByLabel.Sum(s => s.AmountMinor));
    }

    [Fact]
    public async Task NFR_3_9_no_single_label_ever_exceeds_the_total_it_is_reported_against()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 500_000_00);
        var (_, cardParty) = await AddContainerAsync(f, "Card", ContainerKind.CreditCard, 0);
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");

        var labels = await f.Labels.ListAsync();

        // A deliberately messy month: heavy overlap, some unlabelled, and a bill payment
        // that must stay out of both figures.
        for (var i = 1; i <= 12; i++)
        {
            var chosen = labels.Take(i % 4).Select(l => l.Id).ToList();
            await f.Capture.CaptureAsync(new CaptureRequest(
                i % 3 == 0 ? cardParty : bankParty, shop.Id, i * 137_00, "INR", i * 137_00, "INR",
                Today.AddDays(-i), LabelIds: chosen.Count == 0 ? null : chosen));
        }

        await TransferAsync(f, bankParty, cardParty, 5_000_00);

        var spend = await f.Transactions.SpendByLabelAsync(new TransactionFilter { From = Today.AddDays(-30), To = Today });

        // Summing the buckets is meaningless now, but this still has to hold: a label is a
        // subset of the spend, so no bar can be longer than the whole.
        Assert.All(spend.ByLabel, s => Assert.True(s.AmountMinor <= spend.TotalMinor));

        // And the total is what an independent count over the transactions says it is.
        var rows = await f.Transactions.QueryAsync(TransactionFilter.Empty);
        var expected = rows
            .Where(t => t.Kind == TransactionKind.Debit)
            .Sum(t => t.SourceAmountMinor);

        Assert.Equal(expected, spend.TotalMinor);
    }

    [Fact]
    public async Task An_unlabelled_transaction_gets_its_own_bucket()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 50_000_00);
        var shop = await f.Parties.GetOrCreateExternalAsync("Chai");

        await SpendAsync(f, bankParty, shop, 40_00);

        var spend = await f.Transactions.SpendByLabelAsync(new TransactionFilter { From = Today.AddDays(-30), To = Today });

        // Labels are optional, so unlabelled is common. Without a bucket the money would
        // vanish from the breakdown while still counting toward the total.
        var unlabelled = Assert.Single(spend.ByLabel, s => s.LabelId is null);
        Assert.Equal(40_00, unlabelled.AmountMinor);
    }

    [Fact]
    public async Task Filtering_by_label_matches_any_of_them()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 50_000_00);
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");

        var labels = await f.Labels.ListAsync();
        var food = labels.First(l => l.Name == "Food");
        var travel = labels.First(l => l.Name == "Travel");

        await f.Capture.CaptureAsync(new CaptureRequest(
            bankParty, shop.Id, 100_00, "INR", 100_00, "INR", Today, LabelIds: [food.Id]));
        await f.Capture.CaptureAsync(new CaptureRequest(
            bankParty, shop.Id, 200_00, "INR", 200_00, "INR", Today, LabelIds: [travel.Id]));
        await f.Capture.CaptureAsync(new CaptureRequest(
            bankParty, shop.Id, 300_00, "INR", 300_00, "INR", Today));

        var rows = await f.Transactions.QueryAsync(new TransactionFilter { LabelIds = [food.Id, travel.Id] });

        // Any, not all. "Show me anything to do with Food or Travel" is the question people
        // ask; "things that are simultaneously both" is not.
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task Unlabelled_is_filterable_on_its_own()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 50_000_00);
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");

        var food = (await f.Labels.ListAsync()).First(l => l.Name == "Food");

        await f.Capture.CaptureAsync(new CaptureRequest(
            bankParty, shop.Id, 100_00, "INR", 100_00, "INR", Today, LabelIds: [food.Id]));
        await SpendAsync(f, bankParty, shop, 300_00);

        var rows = await f.Transactions.QueryAsync(new TransactionFilter { UnlabelledOnly = true });

        // Otherwise the only way to find them is to notice their absence from every other
        // filter, which is not a way to find anything.
        var only = Assert.Single(rows);
        Assert.Equal(300_00, only.SourceAmountMinor);
    }

    [Fact]
    public async Task Container_filter_matches_either_end_so_it_reads_as_a_statement()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (bank, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 50_000_00);
        var employer = await f.Parties.GetOrCreateExternalAsync("Employer");
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");

        await EarnAsync(f, employer, bankParty, 80_000_00);
        await SpendAsync(f, bankParty, shop, 900_00);

        var rows = await f.Transactions.QueryAsync(
            new TransactionFilter { ContainerIds = [bank.Id] });

        // Both the credit in and the debit out. Money that arrived belongs in the statement
        // just as much as money that left.
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task X1_export_returns_exactly_the_rows_the_list_shows_in_the_same_order()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (bank, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 90_000_00);
        var (_, cashParty) = await AddContainerAsync(f, "Wallet", ContainerKind.Cash, 2_000_00);
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");

        for (var i = 1; i <= 6; i++)
        {
            await SpendAsync(f, i % 2 == 0 ? bankParty : cashParty, shop, i * 100_00, Today.AddDays(-i));
        }

        var filter = new TransactionFilter { ContainerIds = [bank.Id] };

        var listed = await f.Transactions.QueryAsync(filter);
        var export = await f.Exporter.ExportAsync(filter);

        // Same count, and the CSV body rows line up with the list row for row.
        Assert.Equal(listed.Count, export.RowCount);

        var csv = System.Text.Encoding.UTF8.GetString(export.Contents.Span);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(listed.Count + 1, lines.Length);

        for (var i = 0; i < listed.Count; i++)
        {
            var expectedDate = listed[i].OccurredOn.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            Assert.StartsWith(expectedDate, lines[i + 1], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task X2_an_empty_filter_exports_everything()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 90_000_00);
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");

        await SpendAsync(f, bankParty, shop, 100_00);
        await SpendAsync(f, bankParty, shop, 200_00);
        await SpendAsync(f, bankParty, shop, 300_00);

        var export = await f.Exporter.ExportAsync(TransactionFilter.Empty);

        Assert.Equal(3, export.RowCount);
        Assert.Contains("elefi-all-", export.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Csv_starts_with_a_utf8_bom_so_excel_renders_the_rupee_sign()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var export = await f.Exporter.ExportAsync(TransactionFilter.Empty);

        var bom = export.Contents.Span[..3].ToArray();

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bom);
    }

    [Fact]
    public async Task T9_a_soft_deleted_transaction_leaves_the_balance_and_the_export_at_once()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (bank, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 10_000_00);
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");

        var result = await SpendAsync(f, bankParty, shop, 1_000_00);
        Assert.True(result.Succeeded);

        await f.Transactions.SoftDeleteAsync(result.Transaction!.Id);

        var balances = await f.Transactions.BalancesAsync();
        Assert.Equal(10_000_00, balances.Single(b => b.ContainerId == bank.Id).Balance.Minor);

        var export = await f.Exporter.ExportAsync(TransactionFilter.Empty);
        Assert.Equal(0, export.RowCount);

        // Nothing was destroyed: restoring puts it back in both.
        await f.Transactions.RestoreAsync(result.Transaction.Id);

        balances = await f.Transactions.BalancesAsync();
        Assert.Equal(9_000_00, balances.Single(b => b.ContainerId == bank.Id).Balance.Minor);
    }

    [Fact]
    public async Task T7_a_future_dated_transaction_is_refused_with_a_reason()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bankParty) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 10_000_00);
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");

        var result = await SpendAsync(f, bankParty, shop, 100_00, Today.AddDays(1));

        Assert.False(result.Succeeded);
        Assert.Contains("future", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task T1_external_to_external_is_refused_because_it_is_not_the_users_money()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var a = await f.Parties.GetOrCreateExternalAsync("Rahul");
        var b = await f.Parties.GetOrCreateExternalAsync("Priya");

        var result = await f.Capture.CaptureAsync(new CaptureRequest(
            a.Id, b.Id, 100_00, "INR", 100_00, "INR", Today));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task P4_party_names_differing_only_by_case_resolve_to_one_party()
    {
        await using var f = await TestDatabase.CreateAsync(Today);

        var first = await f.Parties.GetOrCreateExternalAsync("Rahul");
        var second = await f.Parties.GetOrCreateExternalAsync("  rahul ");

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task The_total_adds_credits_subtracts_debits_and_ignores_transfers_across_the_whole_filter()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bank) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 10_000_00);
        var (_, card) = await AddContainerAsync(f, "Card", ContainerKind.CreditCard, 0);
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");
        var employer = await f.Parties.GetOrCreateExternalAsync("Employer");

        await SpendAsync(f, bank, shop, 300_00);
        await SpendAsync(f, card, shop, 200_00);
        await EarnAsync(f, employer, bank, 1_000_00);
        await TransferAsync(f, bank, card, 200_00);

        var totals = await f.Transactions.TotalsAsync(TransactionFilter.Empty);

        // D1: the card bill moves nothing in or out.
        Assert.Equal(1_000_00, totals.InMinor);
        Assert.Equal(500_00, totals.OutMinor);
        Assert.Equal(500_00, totals.NetMinor);

        var shopOnly = await f.Transactions.TotalsAsync(new TransactionFilter { PartyIds = [shop.Id] });
        Assert.Equal(-500_00, shopOnly.NetMinor);
    }

    [Fact]
    public async Task A_time_range_cuts_only_its_boundary_days_and_keeps_untimed_transactions()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bank) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 10_000_00);
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");
        var yesterday = Today.AddDays(-1);

        await AtAsync(yesterday, new TimeOnly(8, 0), 1_00);   // before the start time
        await AtAsync(yesterday, new TimeOnly(19, 0), 2_00);  // after the start time
        await AtAsync(yesterday, null, 4_00);                 // no time: kept
        await AtAsync(Today, new TimeOnly(7, 0), 8_00);       // before the end time
        await AtAsync(Today, new TimeOnly(9, 0), 16_00);      // after the end time

        var rows = await f.Transactions.QueryAsync(new TransactionFilter
        {
            From = yesterday,
            FromTime = new TimeOnly(18, 0),
            To = Today,
            ToTime = new TimeOnly(8, 30),
        });

        Assert.Equal([2_00, 4_00, 8_00], rows.Select(r => r.SourceAmountMinor).Order());

        Task AtAsync(DateOnly on, TimeOnly? at, long minor) =>
            f.Capture.CaptureAsync(new CaptureRequest(bank, shop.Id, minor, "INR", minor, "INR", on, at));
    }

    [Fact]
    public async Task Deleted_transactions_are_listed_for_restoring_and_nowhere_else()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (_, bank) = await AddContainerAsync(f, "HDFC", ContainerKind.BankAccount, 10_000_00);
        var shop = await f.Parties.GetOrCreateExternalAsync("Shop");
        var spent = await SpendAsync(f, bank, shop, 300_00);

        await f.Editing.DeleteAsync(spent.Transaction!.Id);

        Assert.Empty(await f.Transactions.QueryAsync(TransactionFilter.Empty));
        var deleted = Assert.Single(await f.Transactions.ListDeletedAsync());
        Assert.Equal("Shop", deleted.DestinationParty!.Name);

        await f.Editing.RestoreAsync(deleted.Id);

        var back = Assert.Single(await f.Transactions.QueryAsync(TransactionFilter.Empty));
        Assert.True(back.NeedsReview);
        Assert.Empty(await f.Transactions.ListDeletedAsync());
    }

    [Fact]
    public async Task Balances_come_back_in_picker_order_with_the_bank_and_last_four()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        await f.ContainerService.CreateAsync(new CreateContainerRequest("Savings", ContainerKind.BankAccount, "INR", 0, Today, "HDFC Bank", "4417"));
        await f.ContainerService.CreateAsync(new CreateContainerRequest("Amex", ContainerKind.CreditCard, "INR", 0, Today));

        var balances = await f.Transactions.BalancesAsync();

        Assert.Equal(["Amex", "Savings"], balances.Select(b => b.Name));
        Assert.Equal("HDFC Bank ••4417", balances[1].Detail);
    }

    private static async Task<(Container Container, Guid PartyId)> AddContainerAsync(
        TestDatabase f, string name, ContainerKind kind, long openingMinor)
    {
        var result = await f.ContainerService.CreateAsync(
            new CreateContainerRequest(name, kind, "INR", openingMinor, Today.AddYears(-1)));

        Assert.True(result.Succeeded, result.Error);

        var party = await f.Containers.PartyForAsync(result.Container!.Id);
        return (result.Container, party!.Id);
    }

    private static Task<CaptureResult> SpendAsync(
        TestDatabase f, Guid sourceParty, EleFi.Domain.Parties.Party destination, long minor, DateOnly? on = null) =>
        f.Capture.CaptureAsync(new CaptureRequest(
            sourceParty, destination.Id, minor, "INR", minor, "INR", on ?? Today));

    private static Task<CaptureResult> EarnAsync(
        TestDatabase f, EleFi.Domain.Parties.Party source, Guid destinationParty, long minor) =>
        f.Capture.CaptureAsync(new CaptureRequest(
            source.Id, destinationParty, minor, "INR", minor, "INR", Today));

    private static Task<CaptureResult> TransferAsync(
        TestDatabase f, Guid sourceParty, Guid destinationParty, long minor) =>
        f.Capture.CaptureAsync(new CaptureRequest(
            sourceParty, destinationParty, minor, "INR", minor, "INR", Today));
}
