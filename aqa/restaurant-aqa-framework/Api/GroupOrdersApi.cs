using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Api;

/// <summary>The group pre-order: the owner's side (bearer = the owner's id token) and the guests' side
/// (bearer = the guest session token that <see cref="CreateGuestSessionAsync"/> hands out). Routes are the ones the
/// storefront calls; the group submit lives under /group-reservations, everything else under /guest.</summary>
public sealed class GroupOrdersApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> GetInviteTokenAsync(string reservationId, string? bearerToken = null) =>
        GetAsync(Url($"/reservations/{reservationId}/invite-token"), new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> GetOwnerSessionIdAsync(string reservationId, string? bearerToken = null) =>
        GetAsync(Url($"/reservations/{reservationId}/guest-session-id"), new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> CreateGuestSessionAsync(CreateGuestSessionRequest request) =>
        Api.PostAsync(Url("/guest/sessions"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(),
        });

    public Task<IAPIResponse> AddDishAsync(string guestSessionId, GuestDishRequest request, string? guestToken = null) =>
        Api.PostAsync(Url($"/guest/sessions/{guestSessionId}/dishes"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(guestToken),
        });

    public Task<IAPIResponse> RemoveDishAsync(string guestSessionId, GuestDishRequest request, string? guestToken = null) =>
        DeleteAsync(Url($"/guest/sessions/{guestSessionId}/dishes"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(guestToken),
        });

    public Task<IAPIResponse> SubmitCartAsync(string guestSessionId, string? guestToken = null) =>
        Api.PostAsync(Url($"/guest/sessions/{guestSessionId}/submit"), new APIRequestContextOptions
        {
            Headers = Headers(guestToken),
        });

    public Task<IAPIResponse> GetGroupAsync(string groupReservationId, string? bearerToken = null) =>
        GetAsync(Url($"/guest/groups/{groupReservationId}/sessions"), new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> GetStatusAsync(string groupReservationId) =>
        GetAsync(Url($"/guest/groups/{groupReservationId}/status"), new APIRequestContextOptions { Headers = Headers() });

    public Task<IAPIResponse> SubmitGroupAsync(string groupReservationId, SubmitGroupRequest request, string? bearerToken = null) =>
        Api.PostAsync(Url($"/group-reservations/{groupReservationId}/submit"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(bearerToken),
        });

    public Task<IAPIResponse> RemoveParticipantAsync(string groupReservationId, string guestSessionId, string? bearerToken = null) =>
        DeleteAsync(Url($"/guest/groups/{groupReservationId}/sessions/{guestSessionId}"), new APIRequestContextOptions
        {
            Headers = Headers(bearerToken),
        });
}
