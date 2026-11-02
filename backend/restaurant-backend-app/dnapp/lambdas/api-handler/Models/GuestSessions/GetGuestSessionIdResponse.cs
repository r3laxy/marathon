using System.Text.Json.Serialization;

namespace APIHandler.Models.GuestSessions;

public record GetGuestSessionIdResponse(
    [property: JsonPropertyName("guestSessionId")] string GuestSessionId);
