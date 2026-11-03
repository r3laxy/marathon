using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>The owner looks at the pre-order of their reservation.</summary>
public sealed class ViewTheOrder(string ownerName) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var owner = actor.Recall<CustomerSession>(ownerName);
        await actor.TheLastResponse().CaptureAsync(
            await new OrdersApi(actor.UsingTheApi()).GetOrderAsync(actor.Recall<GroupOrder>().ReservationId, owner.IdToken));
    }
}
