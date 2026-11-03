using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Screenplay.Questions;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>The standing waiter loads the pre-order of the scenario's reservation, the way the waiter's pre-order page does
/// (GET /orders/{reservationId} with the waiter's token).</summary>
public sealed class ViewTheReservationAsWaiter : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var waiter = await actor.AsksForAsync(new TheWaiterSession());
        await actor.TheLastResponse().CaptureAsync(
            await new OrdersApi(actor.UsingTheApi()).GetOrderAsync(actor.Recall<GroupOrder>().ReservationId, waiter.IdToken));
    }
}
