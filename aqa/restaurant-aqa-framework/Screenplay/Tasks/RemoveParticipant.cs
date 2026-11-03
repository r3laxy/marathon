using Restaurant_Aqa_Framework.Api;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Removes a participant from the group pre-order as whoever holds <paramref name="bearerToken"/>.
/// Either id can be replaced to aim at something that does not exist.</summary>
public sealed class RemoveParticipant(string? bearerToken, string? guestSessionId, string? groupReservationId = null, bool mustSucceed = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var groupId = groupReservationId ?? actor.Recall<GroupOrder>().GroupReservationId;
        var response = actor.TheLastResponse();
        await response.CaptureAsync(
            await new GroupOrdersApi(actor.UsingTheApi()).RemoveParticipantAsync(groupId, guestSessionId ?? "", bearerToken));

        if (mustSucceed && !response.Succeeded)
            throw new InvalidOperationException($"Removing the participant failed: {response.Status} {response.Body}");
    }
}
