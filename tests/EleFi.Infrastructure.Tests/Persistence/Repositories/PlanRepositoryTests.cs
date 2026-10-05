using EleFi.Domain.Planning;
using EleFi.Infrastructure.Persistence.Repositories;
using EleFi.Infrastructure.Tests.Support;

namespace EleFi.Infrastructure.Tests.Persistence.Repositories;

/// <summary>Plans against a real database.</summary>
public class PlanRepositoryTests
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    [Fact]
    public async Task A_repeating_plan_survives_a_round_trip_and_rolls_forward_once_completed()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var repository = new PlanRepository(f.Db, f.Clock);
        var sip = new Plan
        {
            Title = "SIP",
            AmountMinor = 5_000_00,
            DueOn = Today,
            DueTime = new TimeOnly(10, 30),
            AnchorDay = 5,
            Repeat = new RepeatRule(RepeatFrequency.Monthly),
            CreatedAt = f.Clock.UtcNow,
            UpdatedAt = f.Clock.UtcNow,
        };
        sip.SeriesId = sip.Id;
        await repository.AddAsync(sip);

        var next = sip.Complete(f.Clock.UtcNow, Guid.NewGuid())!;
        await repository.UpdateAsync(sip);
        await repository.AddAsync(next);

        f.Db.ChangeTracker.Clear();
        var open = Assert.Single(await repository.ListOpenAsync());
        Assert.Equal(new DateOnly(2026, 11, 5), open.DueOn);
        Assert.Equal(RepeatFrequency.Monthly, open.RepeatFrequency);
        Assert.Equal(new TimeOnly(10, 30), open.DueTime);

        var done = Assert.Single(await repository.ListCompletedAsync());
        Assert.Contains(done.TransactionId!.Value, await repository.ClaimedTransactionIdsAsync());
    }

    [Fact]
    public async Task A_deleted_plan_is_on_neither_list()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var repository = new PlanRepository(f.Db, f.Clock);
        var plan = new Plan { Title = "Rent", DueOn = Today, CreatedAt = f.Clock.UtcNow, UpdatedAt = f.Clock.UtcNow };
        await repository.AddAsync(plan);

        await repository.DeleteAsync(plan.Id);

        Assert.Empty(await repository.ListOpenAsync());
        Assert.Empty(await repository.ListCompletedAsync());
    }
}
