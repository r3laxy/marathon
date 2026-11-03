using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Asks how many people have joined the group pre-order and whether it is still open (public, no sign-in).</summary>
public sealed class CheckTheGroupStatus : ITask
{
    public async Task PerformAsAsync(Actor actor) =>
        await actor.TheLastResponse().CaptureAsync(
            await new GroupOrdersApi(actor.UsingTheApi()).GetStatusAsync(actor.Recall<GroupOrder>().GroupReservationId));
}
