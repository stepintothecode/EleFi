using CsCheck;
using EleFi.Domain.Transactions;

namespace EleFi.Properties.Tests.Transactions;

/// <summary>IT1 for every input: balanced items always add up to their transaction, exactly.</summary>
public class ItemisationTests
{
    private static readonly Gen<long> Total = Gen.Long[1, 100_000_000_00L];

    private static readonly Gen<long[]> Amounts = Gen.Long[1, 50_000_000_00L].Array[1, 12];

    [Fact]
    public void Balanced_items_add_up_to_the_total_or_are_refused_by_exactly_the_excess()
    {
        Gen.Select(Total, Amounts).Sample(t =>
        {
            var (total, amounts) = t;
            var drafts = amounts.Select((a, i) => ItemDraft.Named($"Item {i}", a)).ToList();

            var outcome = Itemisation.Balance(total, drafts);
            var named = amounts.Sum();

            return outcome.Succeeded
                ? outcome.Items.Sum(i => i.AmountMinor) == total
                    && outcome.Items.All(i => i.AmountMinor > 0)
                    && outcome.Items.Count(i => i.IsUnnamed) <= 1
                : outcome.OverByMinor == named - total;
        });
    }
}
