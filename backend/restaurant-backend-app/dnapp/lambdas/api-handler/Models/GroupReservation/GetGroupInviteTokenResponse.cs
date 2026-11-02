namespace APIHandler.Models.GroupReservation;

public record GetGroupInviteTokenResponse(string Token)
{
    public string? GroupReservationId { get; init; }
}