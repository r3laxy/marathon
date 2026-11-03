using Restaurant_Aqa_Framework.Api.Dtos.Orders;

namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

/// <summary>GET /reservations/{id}/invite-token — the link guests use to join, and the id of the group pre-order.</summary>
public sealed record GroupInviteResponse(string Token, string? GroupReservationId);

/// <summary>GET /reservations/{id}/guest-session-id — the owner's own session in the group pre-order.</summary>
public sealed record OwnerSessionResponse(string GuestSessionId);

public sealed record CreateGuestSessionRequest(string GroupReservationId, string GuestName);

/// <summary>POST /guest/sessions — <paramref name="SessionToken"/> is the guest's bearer token for their own cart.</summary>
public sealed record GuestSessionResponse(string SessionId, string SessionToken);

public sealed record GuestDishRequest(string DishId, int Quantity);

public sealed record SubmitGroupRequest(string? ReservationId);

/// <summary><paramref name="Type"/> is "Owner" or "Guest" (the backend serializes the enum by name).</summary>
public sealed record ParticipantResponse(
    string? Id,
    string Type,
    string Name,
    IReadOnlyList<OrderItemResponse> Items,
    decimal Total,
    bool IsSubmitted);

public sealed record GroupSnapshotResponse(IReadOnlyList<ParticipantResponse> Participants, decimal Total);

/// <summary><paramref name="Status"/> is "Open", "Submitted" or "Closed".</summary>
public sealed record GroupStatusResponse(int CurrentParticipants, int MaxCapacity, string Status);

public sealed record SubmittedDishResponse(string DishId, int Quantity);
