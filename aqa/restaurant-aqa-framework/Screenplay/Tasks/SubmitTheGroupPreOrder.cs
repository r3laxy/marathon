using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Confirms the group pre-order as whoever holds <paramref name="bearerToken"/>. <paramref name="reservationId"/>
/// replaces the scenario's own reservation id in the request (null = leave it out, only when <paramref name="useOwn"/> is false).</summary>
public sealed class SubmitTheGroupPreOrder(string? bearerToken, bool useOwn = true, string? reservationId = null, bool mustSucceed = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrder>();
        var body = new SubmitGroupRequest(useOwn ? group.ReservationId : reservationId);
        var response = actor.TheLastResponse();

        await response.CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi()).SubmitGroupAsync(group.GroupReservationId, body, bearerToken));

        if (mustSucceed && !response.Succeeded)
            throw new InvalidOperationException($"Submitting the group pre-order failed: {response.Status} {response.Body}");
    }
}
