using System.Security.Cryptography;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Seeding;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>The owner books the scenario's table (tomorrow, so the pre-order is still editable) for the given number
/// of guests, then opens the group pre-order: the invite and the owner's own session are remembered as the
/// scenario's <see cref="GroupOrder"/>. The reservation is cancelled again when the scenario ends.</summary>
public sealed class BookTableForGroup(string ownerName, int guests, string? groupName = null, bool openGroupPreOrder = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var owner = actor.Recall<CustomerSession>(ownerName);
        // The scenario's table: the standing test table when one is configured, else a table this scenario adds.
        if (!actor.TryRecall<TableResponse>(out _))
            await new AddTable(seats: guests).PerformAsAsync(actor);

        var table = actor.Recall<TableResponse>();
        var reservations = new ReservationsApi(actor.UsingTheApi());

        var booking = await reservations.CreateReservationAsync(
            SampleDataFactory.CreateReservationRequest(table.Id, RandomSlot(), guests), owner.IdToken);
        if (!booking.Ok)
            throw new InvalidOperationException($"Booking the table failed with {booking.Status}: {await booking.TextAsync()}");

        var reservation = await booking.ReadAsAsync<ReservationResponse>();
        actor.Recall<CleanupScope>().TrackDelete(
            $"reservation:{reservation.Id}",
            () => reservations.CancelReservationAsync(reservation.Id, owner.IdToken));

        var groups = new GroupOrdersApi(actor.UsingTheApi());

        var invite = await groups.GetInviteTokenAsync(reservation.Id, owner.IdToken);
        if (!invite.Ok)
            throw new InvalidOperationException($"Getting the invite failed with {invite.Status}: {await invite.TextAsync()}");

        var inviteBody = await invite.ReadAsAsync<GroupInviteResponse>();

        // The owner's own session in the group is only created when the owner first opens the group pre-order.
        var ownerSessionId = "";
        if (openGroupPreOrder)
        {
            var ownerSession = await groups.GetOwnerSessionIdAsync(reservation.Id, owner.IdToken);
            if (!ownerSession.Ok)
                throw new InvalidOperationException($"Opening the owner's session failed with {ownerSession.Status}: {await ownerSession.TextAsync()}");

            ownerSessionId = (await ownerSession.ReadAsAsync<OwnerSessionResponse>()).GuestSessionId;
        }

        var hadAGroup = actor.TryRecall<GroupOrder>(out var earlier);
        var group = new GroupOrder(
            ownerName,
            reservation.Id,
            inviteBody.GroupReservationId ?? throw new InvalidOperationException("The invite carries no group reservation id."),
            inviteBody.Token,
            ownerSessionId);

        if (groupName is null)
        {
            actor.Remember(group);
            return;
        }

        // A further group, kept under its own name: the scenario's own group stays the current one.
        actor.Remember(groupName, group);
        if (hadAGroup)
            actor.Remember(earlier);
    }

    /// <summary>Some day in the next year, at some hour: scenarios run in parallel on one shared table, so each takes
    /// its own slot, and it is far enough ahead for the pre-order to stay editable (it locks 30 minutes before the visit).</summary>
    private static DateTime RandomSlot() =>
        DateTime.UtcNow.Date.AddDays(RandomNumberGenerator.GetInt32(2, 360)).AddHours(RandomNumberGenerator.GetInt32(8, 20));
}
