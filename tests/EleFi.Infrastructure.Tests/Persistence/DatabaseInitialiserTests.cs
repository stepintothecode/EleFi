using EleFi.Domain.Labels;
using EleFi.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Tests.Persistence;

/// <summary>
/// The database comes up encrypted, migrated, and seeded, or the app has nothing to stand
/// on.
/// </summary>
public class DatabaseInitialiserTests
{
    [Fact]
    public async Task Migration_creates_an_encrypted_database_that_opens_with_the_key()
    {
        await using var fixture = await TestDatabase.CreateAsync();

        // If SQLCipher were not active, or the key were not applied before first use, this
        // would fail with "file is not a database" rather than returning a row count.
        var labelCount = await fixture.Db.Labels.CountAsync();

        Assert.True(labelCount > 0);
    }

    [Fact]
    public async Task A_starter_label_set_is_seeded_with_no_undeletable_one_among_them()
    {
        await using var fixture = await TestDatabase.CreateAsync();

        var labels = await fixture.Labels.ListAsync();

        Assert.Contains(labels, l => l.Name == "Food");

        // No Uncategorised. Labels are optional now (ADR-0013), so a transaction with none
        // is simply unlabelled, and a system label nobody could delete was a rule the user
        // had to learn for no benefit.
        Assert.DoesNotContain(labels, l => l.Name == "Uncategorised");
    }

    [Fact]
    public async Task Seeding_is_idempotent_so_a_second_start_does_not_duplicate_labels()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var before = await fixture.Db.Labels.CountAsync();

        await new EleFi.Infrastructure.Persistence.DatabaseInitialiser(fixture.Db, fixture.Clock)
            .InitialiseAsync();

        Assert.Equal(before, await fixture.Db.Labels.CountAsync());
    }

    [Fact]
    public async Task Built_in_parse_rules_are_seeded_and_all_compile()
    {
        await using var fixture = await TestDatabase.CreateAsync();

        var rules = await fixture.Db.ParseRules.ToListAsync();

        Assert.NotEmpty(rules);
        Assert.All(rules, rule =>
        {
            Assert.True(rule.TryCompile(out var compiled, out var error), error);
            Assert.NotNull(compiled);
        });
    }

    [Fact]
    public async Task Rules_shipped_in_a_later_release_reach_an_existing_install()
    {
        await using var fixture = await TestDatabase.CreateAsync();

        // An install from before the Payment App rules existed: drop them, as if never seeded.
        var appRules = await fixture.Db.ParseRules
            .Where(r => r.Channel == EleFi.Domain.Alerts.AlertChannel.PaymentApp)
            .ToListAsync();
        fixture.Db.ParseRules.RemoveRange(appRules);
        await fixture.Db.SaveChangesAsync();

        await new EleFi.Infrastructure.Persistence.DatabaseInitialiser(fixture.Db, fixture.Clock).InitialiseAsync();

        var names = await fixture.Db.ParseRules.Select(r => r.Name).ToListAsync();
        Assert.Equal(EleFi.Domain.Alerts.BuiltInParseRules.All.Count, names.Count);
        Assert.Contains("GPay paid", names);
    }

    [Fact]
    public async Task Reseeding_leaves_a_rule_the_user_disabled_disabled()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var hdfc = await fixture.Db.ParseRules.SingleAsync(r => r.Name == "HDFC debit");
        hdfc.IsEnabled = false;
        await fixture.Db.SaveChangesAsync();

        await new EleFi.Infrastructure.Persistence.DatabaseInitialiser(fixture.Db, fixture.Clock).InitialiseAsync();

        var rules = await fixture.Db.ParseRules.Where(r => r.Name == "HDFC debit").ToListAsync();
        Assert.False(Assert.Single(rules).IsEnabled);
    }
}
