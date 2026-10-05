using EleFi.Application.Containers;
using EleFi.Application.Transactions;
using EleFi.Domain.Containers;
using EleFi.Domain.Goals;
using EleFi.Infrastructure.Persistence.Repositories;
using EleFi.Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace EleFi.Infrastructure.Tests.Persistence.Repositories;

/// <summary>Goals against a real database.</summary>
public class GoalRepositoryTests
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    [Fact]
    public async Task Linked_containers_are_kept_changed_and_read_back()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var (rd, sbi) = (await AddAsync(f, "RD"), await AddAsync(f, "SBI"));
        var repository = new GoalRepository(f.Db, f.Clock);
        var goal = NewGoal(f);
        goal.Containers.Add(new GoalContainer { GoalId = goal.Id, ContainerId = rd });
        await repository.AddAsync(goal);

        var loaded = (await repository.FindAsync(goal.Id))!;
        await repository.UpdateAsync(loaded, [sbi]);

        f.Db.ChangeTracker.Clear();
        var again = (await repository.FindAsync(goal.Id))!;
        Assert.Equal([sbi], again.Containers.Select(c => c.ContainerId));
    }

    [Fact]
    public async Task GL6_deleting_a_goal_clears_it_from_its_transactions_and_leaves_them_alone()
    {
        await using var f = await TestDatabase.CreateAsync(Today);
        var sbi = await AddAsync(f, "SBI");
        var repository = new GoalRepository(f.Db, f.Clock);
        var goal = NewGoal(f);
        await repository.AddAsync(goal);

        var kept = await SpendAsync(f, sbi, 500_00);
        var deleted = await SpendAsync(f, sbi, 200_00);
        await repository.SetGoalAsync(kept, goal.Id);
        await repository.SetGoalAsync(deleted, goal.Id);
        await f.Editing.DeleteAsync(deleted);
        Assert.Single(await repository.ContributionsAsync());

        await repository.DeleteAsync(goal.Id);

        f.Db.ChangeTracker.Clear();
        Assert.Empty(await repository.ListAsync());
        var all = await f.Db.Transactions.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.All(all, t => Assert.Null(t.GoalId));
    }

    private static Goal NewGoal(TestDatabase f) => new()
    {
        Name = "Ring",
        TargetAmountMinor = 3_00_000_00,
        StartDate = Today,
        TargetDate = Today.AddYears(2),
        FundingMode = FundingMode.TracksContributions,
        CreatedAt = f.Clock.UtcNow,
        UpdatedAt = f.Clock.UtcNow,
    };

    private static async Task<Guid> AddAsync(TestDatabase f, string name)
    {
        var result = await f.ContainerService.CreateAsync(new CreateContainerRequest(
            name, ContainerKind.BankAccount, "INR", 1_00_000_00, Today.AddYears(-1)));
        Assert.True(result.Succeeded, result.Error);
        return result.Container!.Id;
    }

    private static async Task<Guid> SpendAsync(TestDatabase f, Guid container, long minor)
    {
        var from = await f.Containers.PartyForAsync(container);
        var shop = await f.Parties.GetOrCreateExternalAsync("Tanishq");
        var result = await f.Capture.CaptureAsync(new CaptureRequest(from!.Id, shop.Id, minor, "INR", minor, "INR", Today));
        Assert.True(result.Succeeded, result.Error);
        return result.Transaction!.Id;
    }
}
