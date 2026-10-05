using EleFi.Ui.Services;

namespace EleFi.Ui.Tests.Services;

public class TransactionListStateTests
{
    [Fact]
    public void The_row_to_return_to_is_handed_over_once()
    {
        var state = new TransactionListState();
        var id = Guid.NewGuid();
        state.ReturnTo = id;

        Assert.Equal(id, state.TakeReturnTo());

        // Marked once on the way back, not every time the list is visited afterwards.
        Assert.Null(state.TakeReturnTo());
    }
}
