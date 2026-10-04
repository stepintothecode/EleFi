using EleFi.Application.Abstractions;
using EleFi.Application.Labels;
using EleFi.Domain.Labels;
using NSubstitute;

namespace EleFi.Application.Tests.Labels;

/// <summary>
/// The order labels are offered in: the ones you actually use, first.
/// </summary>
public class LabelServiceTests
{
    private static readonly Label Food = new() { Name = "Food", SortOrder = 0 };
    private static readonly Label Travel = new() { Name = "Travel", SortOrder = 1 };
    private static readonly Label Rent = new() { Name = "Rent", SortOrder = 2 };
    private static readonly Label Bills = new() { Name = "Bills", SortOrder = 3 };

    [Fact]
    public async Task Labels_come_back_most_used_first()
    {
        var service = Service(
            [Food, Travel, Rent, Bills],
            new Dictionary<Guid, int> { [Rent.Id] = 9, [Travel.Id] = 4 });

        var ordered = await service.ListByUsageAsync();

        Assert.Equal(["Rent", "Travel", "Food", "Bills"], ordered.Select(l => l.Name));
    }

    [Fact]
    public async Task Unused_labels_keep_their_own_order_after_the_used_ones()
    {
        var service = Service([Bills, Food, Travel], new Dictionary<Guid, int>());

        var ordered = await service.ListByUsageAsync();

        // Nothing used yet, so the user's sort order is all there is to go on.
        Assert.Equal(["Food", "Travel", "Bills"], ordered.Select(l => l.Name));
    }

    [Fact]
    public async Task Equal_usage_falls_back_to_the_users_order()
    {
        var service = Service(
            [Travel, Food],
            new Dictionary<Guid, int> { [Travel.Id] = 2, [Food.Id] = 2 });

        var ordered = await service.ListByUsageAsync();

        Assert.Equal(["Food", "Travel"], ordered.Select(l => l.Name));
    }

    private static LabelService Service(IReadOnlyList<Label> labels, IReadOnlyDictionary<Guid, int> counts)
    {
        var repository = Substitute.For<ILabelRepository>();
        repository.ListAsync(Arg.Any<CancellationToken>()).Returns(labels);
        repository.UsageCountsAsync(Arg.Any<CancellationToken>()).Returns(counts);

        return new LabelService(repository, Substitute.For<IClock>());
    }
}
