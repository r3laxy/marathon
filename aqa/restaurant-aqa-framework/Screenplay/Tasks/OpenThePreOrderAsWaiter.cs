using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>The signed-in waiter opens the pre-order page of the scenario's reservation.</summary>
public sealed class OpenThePreOrderAsWaiter(string customerName) : ITask
{
    public Task PerformAsAsync(Actor actor) =>
        new WaiterPreOrderPage(actor.LookingAtThePage())
            .OpenAsync(actor.Recall<GroupOrder>().ReservationId, customerName, "AQA table");
}
