using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>A guest joins the group pre-order through the invite. The outcome is the scenario's last response; when
/// they were let in, the guest is remembered under their name and removed again when the scenario ends.</summary>
public sealed class JoinTheGroupPreOrder(string guestName, bool mustSucceed = true, string? groupName = null) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrder>(groupName);
        var groups = new GroupOrdersApi(actor.UsingTheApi());
        var response = actor.TheLastResponse();

        await response.CaptureAsync(await groups.CreateGuestSessionAsync(new CreateGuestSessionRequest(group.GroupReservationId, guestName)));
        if (!response.Succeeded)
        {
            if (mustSucceed)
                throw new InvalidOperationException($"{guestName} could not join: {response.Status} {response.Body}");

            return;
        }

        var session = response.Json<GuestSessionResponse>();
        actor.Remember(guestName, new GuestSession(guestName, session.SessionId, session.SessionToken));

        var owner = actor.Recall<CustomerSession>(group.OwnerName);
        actor.Recall<CleanupScope>().TrackDelete(
            $"guest-session:{session.SessionId}",
            () => groups.RemoveParticipantAsync(group.GroupReservationId, session.SessionId, owner.IdToken));
    }
}
