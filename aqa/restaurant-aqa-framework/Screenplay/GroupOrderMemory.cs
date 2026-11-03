using Restaurant_Aqa_Framework.Api.Dtos.Auth;

namespace Restaurant_Aqa_Framework.Screenplay;

/// <summary>A customer who registered and signed in during the scenario (remembered under their name).</summary>
public sealed record CustomerSession(string Name, RegisterRequest Details, string IdToken);

/// <summary>The group pre-order of the table the scenario's owner booked.</summary>
public sealed record GroupOrder(
    string OwnerName,
    string ReservationId,
    string GroupReservationId,
    string InviteToken,
    string OwnerSessionId);

/// <summary>A guest who joined the group pre-order (remembered under their name).</summary>
public sealed record GuestSession(string Name, string SessionId, string Token);
