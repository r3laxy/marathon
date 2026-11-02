using System.Text.Json.Serialization;

namespace APIHandler.Models.GroupReservation;

public record SubmitGroupReservationRequest(
    [property: JsonPropertyName("reservationId")] string? ReservationId);
