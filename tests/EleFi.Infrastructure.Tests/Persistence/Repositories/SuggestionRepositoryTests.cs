using EleFi.Application.Containers;
using EleFi.Domain.Alerts;
using EleFi.Domain.Containers;
using EleFi.Domain.Money;
using EleFi.Infrastructure.Persistence.Repositories;
using EleFi.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Tests.Persistence.Repositories;

/// <summary>
/// Suggestions against a real database: what is stored, and what is not.
/// </summary>
public class SuggestionRepositoryTests
{
    private static readonly DateOnly Today = new(2026, 9, 1);

    [Fact]
    public async Task A_merged_suggestion_keeps_both_channels_the_app_and_the_note()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        await AddCardAsync(f);

        await f.Suggestions.IngestAsync(
            "VM-HDFCBK", "Rs.450.00 debited from a/c XX4417 on 01-09-26 to ZOMATO", f.Clock.UtcNow, Currency.Inr);
        await f.Suggestions.IngestAsync(
            AlertChannel.PaymentApp, PaymentApps.GPay.Package, "Paid ₹450 to Zomato for Lunch", f.Clock.UtcNow, Currency.Inr);

        f.Db.ChangeTracker.Clear();
        var stored = await f.Db.CaptureSuggestions.SingleAsync();

        Assert.Equal(AlertEvidence.Sms | AlertEvidence.PaymentApp, stored.Evidence);
        Assert.Equal("GPay", stored.PaymentAppName);
        Assert.Equal("Lunch", stored.Note);
        Assert.Equal("Zomato", stored.CounterpartyText);
        Assert.NotNull(stored.ContainerId);
    }

    [Fact]
    public async Task SM6_a_merged_alerts_fingerprint_counts_as_already_offered()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var repository = new SuggestionRepository(f.Db);

        await repository.AddAsync(new CaptureSuggestion
        {
            Fingerprint = "sms",
            CorroboratingFingerprint = "app",
            AmountMinor = 100,
            CreatedAt = f.Clock.UtcNow,
            ExpiresAt = f.Clock.UtcNow.AddDays(7),
        });

        Assert.True(await repository.ExistsAsync("sms"));
        Assert.True(await repository.ExistsAsync("app"));
        Assert.False(await repository.ExistsAsync("other"));
        Assert.Equal(1, await repository.CountPendingAsync());
    }

    [Fact]
    public async Task SM1_no_column_anywhere_can_hold_a_message_body_or_a_sender()
    {
        await using var f = await TestDatabase.CreateAsync(Today);

        var columns = f.Db.Model.FindEntityType(typeof(CaptureSuggestion))!
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        // The new columns hold what a rule extracted, never the text it was extracted from.
        Assert.DoesNotContain(columns, c => c.Contains("Body", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("Sender", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(columns, c => c.Contains("Text", StringComparison.OrdinalIgnoreCase) && c != "CounterpartyText");
    }

    private static async Task AddCardAsync(TestDatabase f)
    {
        var result = await f.ContainerService.CreateAsync(new CreateContainerRequest(
            "HDFC Card", ContainerKind.CreditCard, "INR", 0, Today.AddYears(-1), AccountNumberLast4: "4417"));

        Assert.True(result.Succeeded, result.Error);
    }
}
