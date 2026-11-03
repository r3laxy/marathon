using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>The owner places their own (regular) pre-order of one portion of a dish — the order the group's dishes are
/// added to when the group pre-order is submitted.</summary>
public sealed class PlaceTheOwnersOrder(string ownerName, int dishNumber) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var owner = actor.Recall<CustomerSession>(ownerName);
        var group = actor.Recall<GroupOrder>();
        var dish = actor.RecallAll<DishResponse>()[dishNumber - 1];
        var orders = new OrdersApi(actor.UsingTheApi());

        var response = await orders.CreateOrderAsync(
            new CreateOrderRequest(group.ReservationId, [new OrderItemRequest(dish.Id, 1)]), owner.IdToken);
        if (!response.Ok)
            throw new InvalidOperationException($"Placing the order failed with {response.Status}: {await response.TextAsync()}");

        actor.Recall<CleanupScope>().TrackDelete(
            $"order:{group.ReservationId}",
            () => orders.DeleteOrderAsync(group.ReservationId, owner.IdToken));
    }
}
