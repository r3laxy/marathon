using APIHandler.Models.GroupReservation;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIHandler.Services.GroupReservations;

public interface IGroupSubmissionService
{
    /// <summary>
    /// Consolidates dishes of all submitted guest sessions into the reservation's order,
    /// marks the order as submitted by customer and the group reservation as submitted.
    /// </summary>
    Task<IReadOnlyList<SubmittedDishResponse>> SubmitGroupPreOrderAsync(string userId, string groupReservationId, string reservationId);
}
