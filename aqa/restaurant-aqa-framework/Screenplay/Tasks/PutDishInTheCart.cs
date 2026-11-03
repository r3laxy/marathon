using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>A guest puts portions of a dish in a cart — their own, or (to test access) another guest's cart, using
/// their own token. The outcome is the scenario's last response; a set-up step passes mustSucceed.</summary>
public sealed class PutDishInTheCart(
    string guestName, int dishNumber, int portions, string? intoCartOf = null, bool mustSucceed = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var guest = actor.Recall<GuestSession>(guestName);
        var cart = actor.Recall<GuestSession>(intoCartOf ?? guestName);
        var dish = actor.RecallAll<DishResponse>()[dishNumber - 1];
        var response = actor.TheLastResponse();

        var groups = new GroupOrdersApi(actor.UsingTheApi());

        // A dish added a moment ago may not be listed for its location yet (the backend reads it through an eventually
        // consistent index), so that one refusal is retried for up to a minute, and so is a gateway timeout (5xx) from the busy
        // develop backend; every other answer is final.
        var started = DateTime.UtcNow;
        for (var attempt = 1; ; attempt++)
        {
            await response.CaptureAsync(await groups.AddDishAsync(cart.SessionId, new GuestDishRequest(dish.Id, portions), guest.Token));
            var transient = response.Status >= 500 || response.Body.Contains("not available at this location", StringComparison.Ordinal);
            if (response.Succeeded || !transient || DateTime.UtcNow - started > TimeSpan.FromSeconds(60))
                break;

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        if (mustSucceed && !response.Succeeded)
            throw new InvalidOperationException($"{guestName} could not add the dish: {response.Status} {response.Body}");
    }
}
