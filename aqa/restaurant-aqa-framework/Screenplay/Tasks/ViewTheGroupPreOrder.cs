using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Asks for the group pre-order as whoever holds <paramref name="bearerToken"/> (nobody, when null). When
/// <paramref name="groupReservationId"/> is given it is asked for instead of the scenario's own group.</summary>
public sealed class ViewTheGroupPreOrder(string? bearerToken, string? groupReservationId = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var id = groupReservationId ?? actor.Recall<GroupOrder>().GroupReservationId;
        await actor.TheLastResponse().CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi()).GetGroupAsync(id, bearerToken));
    }
}
