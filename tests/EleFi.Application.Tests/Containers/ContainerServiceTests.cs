using EleFi.Application.Abstractions;
using EleFi.Application.Containers;
using EleFi.Application.Tests.Support;
using EleFi.Domain.Containers;
using NSubstitute;

namespace EleFi.Application.Tests.Containers;

/// <summary>Containers: the rules around deleting one, and undoing that.</summary>
public class ContainerServiceTests
{
    private readonly IContainerRepository _containers = Substitute.For<IContainerRepository>();
    private readonly Container _wallet = new() { Name = "Paytm Wallet", Kind = ContainerKind.Wallet };

    public ContainerServiceTests() =>
        _containers.FindAsync(_wallet.Id, Arg.Any<CancellationToken>()).Returns(_wallet);

    [Fact]
    public async Task C4_a_container_with_transactions_is_never_deleted()
    {
        _containers.TransactionCountAsync(_wallet.Id, Arg.Any<CancellationToken>()).Returns(3);

        var result = await Service().DeleteAsync(_wallet.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("Archive", result.Error, StringComparison.Ordinal);
        await _containers.DidNotReceive().SoftDeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unused_container_can_be_deleted_and_the_delete_undone()
    {
        var service = Service();

        Assert.True((await service.DeleteAsync(_wallet.Id)).Succeeded);
        await service.RestoreAsync(_wallet.Id);

        await _containers.Received(1).SoftDeleteAsync(_wallet.Id, Arg.Any<CancellationToken>());
        await _containers.Received(1).RestoreAsync(_wallet.Id, Arg.Any<CancellationToken>());
    }

    private ContainerService Service() => new(_containers, Substitute.For<ITransactionRepository>(), new MovableClock());
}
