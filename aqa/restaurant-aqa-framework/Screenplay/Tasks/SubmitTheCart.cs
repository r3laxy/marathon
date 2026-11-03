using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>A guest confirms their cart. The outcome is the scenario's last response; a set-up step passes mustSucceed.</summary>
public sealed class SubmitTheCart(string guestName, bool mustSucceed = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var guest = actor.Recall<GuestSession>(guestName);
        var response = actor.TheLastResponse();

        await response.CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi()).SubmitCartAsync(guest.SessionId, guest.Token));

        if (mustSucceed && !response.Succeeded)
            throw new InvalidOperationException($"{guestName} could not submit the cart: {response.Status} {response.Body}");
    }
}
