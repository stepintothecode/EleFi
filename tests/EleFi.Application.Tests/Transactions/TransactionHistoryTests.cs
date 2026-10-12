using EleFi.Application.Transactions;
using EleFi.Domain.Audit;
using EleFi.Domain.Money;

namespace EleFi.Application.Tests.Transactions;

/// <summary>
/// A transaction's history reads in names and money, never in the ids and minor units the
/// triggers store.
/// </summary>
public class TransactionHistoryTests
{
    private static readonly Guid Zomato = Guid.CreateVersion7();
    private static readonly Guid Swiggy = Guid.CreateVersion7();
    private static readonly Guid HdfcCard = Guid.CreateVersion7();
    private static readonly Guid GPay = Guid.CreateVersion7();

    private static readonly Dictionary<Guid, string> Names = new()
    {
        [Zomato] = "Zomato",
        [Swiggy] = "Swiggy",
        [HdfcCard] = "HDFC Card",
        [GPay] = "GPay",
    };

    [Fact]
    public void A_changed_payee_reads_as_the_two_names()
    {
        var change = Single(Updated(("DestinationPartyId", Zomato.ToString(), Swiggy.ToString())));

        Assert.Equal(("Paid to", "Zomato", "Swiggy"), (change.Field, change.From, change.To));
    }

    [Fact]
    public void A_container_end_reads_as_the_containers_name()
    {
        var change = Single(Updated(("SourcePartyId", HdfcCard.ToString(), Zomato.ToString())));

        Assert.Equal("HDFC Card", change.From);
    }

    [Fact]
    public void An_app_reads_as_its_name_and_none_reads_as_nothing()
    {
        var change = Single(Updated(("PaymentAppId", null, GPay.ToString())));

        Assert.Equal(("Paid with", "nothing", "GPay"), (change.Field, change.From, change.To));
    }

    [Fact]
    public void An_id_that_no_longer_resolves_never_shows_as_an_id()
    {
        var gone = Guid.CreateVersion7();
        var change = Single(Updated(("DestinationPartyId", gone.ToString(), Zomato.ToString())));

        Assert.DoesNotContain(gone.ToString(), change.From, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("someone no longer on record", change.From);
    }

    [Fact]
    public void Amounts_read_as_money_and_the_mirrored_destination_amount_is_not_repeated()
    {
        var entry = Assert.Single(Describe(Updated(
            ("SourceAmountMinor", "45000", "54000"),
            ("DestinationAmountMinor", "45000", "54000"))));

        var amount = Assert.Single(entry.Changes);
        Assert.Equal("Amount", amount.Field);
        Assert.Equal(MoneyText.ToDisplayString(Money.FromMinor(45000, Currency.Inr)), amount.From);
        Assert.Equal(MoneyText.ToDisplayString(Money.FromMinor(54000, Currency.Inr)), amount.To);
    }

    [Fact]
    public void Dates_flags_and_capture_sources_read_as_words()
    {
        var entry = Assert.Single(Describe(Updated(
            ("OccurredOn", "2026-08-29", "2026-08-30"),
            ("NeedsReview", "1", "0"),
            ("CaptureSource", "6", "0"))));

        Assert.Contains(entry.Changes, c => c is { Field: "Date", From: "29 Aug 2026", To: "30 Aug 2026" });
        Assert.Contains(entry.Changes, c => c is { Field: "Needs review", From: "yes", To: "no" });
        Assert.Contains(entry.Changes, c => c is { Field: "Recorded via", From: "a bank SMS", To: "the app" });
    }

    [Fact]
    public void A_plans_history_reads_as_words_too()
    {
        var sbi = Guid.CreateVersion7();
        var names = new Dictionary<Guid, string> { [sbi] = "SBI" };

        var entry = Assert.Single(TransactionHistory.Describe(Updated(
            ("DueOn", "2026-10-05", "2026-10-07"),
            ("RepeatFrequency", "0", "3"),
            ("ContainerId", null, sbi.ToString()),
            ("IsSkipped", "0", "1")), names, Currency.Inr));

        Assert.Contains(entry.Changes, c => c is { Field: "Due", From: "5 Oct 2026", To: "7 Oct 2026" });
        Assert.Contains(entry.Changes, c => c is { Field: "Repeats", From: "never", To: "monthly" });
        Assert.Contains(entry.Changes, c => c is { Field: "Container", From: "nothing", To: "SBI" });
        Assert.Contains(entry.Changes, c => c is { Field: "Skipped", From: "no", To: "yes" });
    }

    [Fact]
    public void How_it_was_recorded_reads_as_words_not_a_number()
    {
        var created = new AuditEvent { Action = AuditAction.Created, CaptureSource = "1" };

        var entry = Assert.Single(Describe([created]));

        Assert.Equal("quick capture", entry.Via);
    }

    [Fact]
    public void The_ids_a_history_needs_named_are_the_parties_and_apps_it_mentions()
    {
        var ids = TransactionHistory.ReferencedIds(Updated(
            ("DestinationPartyId", Zomato.ToString(), Swiggy.ToString()),
            ("MarketplaceAppId", null, GPay.ToString()),
            ("Description", "a", "b")));

        Assert.Equal(new[] { Zomato, Swiggy, GPay }.Order(), ids.Order());
    }

    private static HistoryChange Single(AuditEvent[] events) =>
        Assert.Single(Assert.Single(Describe(events)).Changes);

    private static IReadOnlyList<HistoryEntry> Describe(IEnumerable<AuditEvent> events) =>
        TransactionHistory.Describe(events, Names, Currency.Inr);

    private static AuditEvent[] Updated(params (string Field, string? From, string? To)[] changes) =>
    [
        new AuditEvent
        {
            Action = AuditAction.Updated,
            Changes = "[" + string.Join(",", changes.Select(c =>
                $"{{\"field\":\"{c.Field}\",\"from\":{Json(c.From)},\"to\":{Json(c.To)}}}")) + "]",
        },
    ];

    private static string Json(string? value) => value is null ? "null" : $"\"{value}\"";
}
