using EleFi.Application.Abstractions;
using EleFi.Application.Suggestions;
using EleFi.Application.Tests.Support;
using EleFi.Domain.Alerts;
using EleFi.Domain.Transactions;

namespace EleFi.Application.Tests.Suggestions;

/// <summary>Teaching EleFi a message format, and managing what it was taught.</summary>
public class ParseRuleServiceTests
{
    private readonly MemoryRules _rules = new();

    [Fact]
    public async Task A_good_example_is_saved_as_the_users_own_rule()
    {
        var taught = await Service().TeachAsync(new TeachingExample(
            "VM-HDFCBK", "Rs.450.00 debited from a/c XX4417 to ZOMATO. Avl bal Rs.100.00",
            TransactionKind.Debit, "450.00", "ZOMATO", "4417"));

        Assert.True(taught.Succeeded, taught.Error);
        var saved = Assert.Single(await Service().TaughtAsync());
        Assert.False(saved.IsBuiltIn);
    }

    [Fact]
    public async Task A_bad_example_saves_nothing()
    {
        var taught = await Service().TeachAsync(new TeachingExample(
            "VM-HDFCBK", "Rs.450.00 debited", TransactionKind.Debit, "999.00"));

        Assert.False(taught.Succeeded);
        Assert.Empty(_rules.Rows);
    }

    [Fact]
    public async Task Taught_rules_are_listed_without_the_built_ins()
    {
        _rules.Rows.AddRange(BuiltInParseRules.Create(DateTimeOffset.UnixEpoch));

        Assert.Empty(await Service().TaughtAsync());
    }

    [Fact]
    public async Task A_built_in_rule_cannot_be_forgotten_only_switched_off()
    {
        var builtIn = BuiltInParseRules.Create(DateTimeOffset.UnixEpoch)[0];
        _rules.Rows.Add(builtIn);

        Assert.False(await Service().ForgetAsync(builtIn.Id));

        await Service().SetEnabledAsync(builtIn.Id, false);
        Assert.False(builtIn.IsEnabled);
    }

    [Fact]
    public async Task A_taught_rule_can_be_forgotten()
    {
        var rule = new ParseRule { Name = "Mine", IsBuiltIn = false };
        _rules.Rows.Add(rule);

        Assert.True(await Service().ForgetAsync(rule.Id));
        Assert.NotNull(rule.DeletedAt);
    }

    private ParseRuleService Service() => new(_rules, new MovableClock());

    private sealed class MemoryRules : IParseRuleRepository
    {
        public List<ParseRule> Rows { get; } = [];

        public Task<IReadOnlyList<ParseRule>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ParseRule>>(Rows.Where(r => r.DeletedAt is null).ToList());

        public Task<ParseRule?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.Find(r => r.Id == id));

        public Task AddAsync(ParseRule rule, CancellationToken cancellationToken = default)
        {
            Rows.Add(rule);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ParseRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Rows.Find(r => r.Id == id)!.DeletedAt = DateTimeOffset.UnixEpoch;
            return Task.CompletedTask;
        }
    }
}
