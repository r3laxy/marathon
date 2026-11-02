using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Services.GroupReservations;

public class GroupSubmissionService(
    IGroupReservationRepository groupReservationRepository,
    IGuestSessionRepository guestSessionRepository,
    IReservationRepository reservationRepository,
    IOrderRepository orderRepository) : IGroupSubmissionService
{
    public async Task<IReadOnlyList<SubmittedDishResponse>> SubmitGroupPreOrderAsync(
        string userId, string groupReservationId, string reservationId)
    {
        Reservation reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException($"Reservation {reservationId} was not found.");

        GroupReservation group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();

        if (!string.Equals(reservation.CustomerId, userId, StringComparison.OrdinalIgnoreCase))
            throw new ActionForbiddenException("Only the reservation owner can perform this action.");

        if (group.ReservationId != reservation.Id)
            throw new ValidationException("Group reservation does not belong to the given reservation.");

        if (group.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();

        if (group.Status == GroupReservationStatus.Closed)
            throw new GroupReservationExpiredException();

        List<GuestSession> sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(group.GroupReservationId);
        List<GuestSession> submittedSessions = sessions.Where(s => s.IsSubmitted).ToList();

        if (submittedSessions.Count == 0)
            throw new ValidationException("There are no submitted guest sessions in the group.");

        List<SubmittedDishResponse> mergedDishes = submittedSessions
            .SelectMany(s => s.Dishes ?? new List<OrderItem>())
            .Where(d => d.Quantity > 0)
            .GroupBy(d => d.DishId)
            .Select(g => new SubmittedDishResponse(g.Key, g.Sum(d => d.Quantity)))
            .ToList();

        if (mergedDishes.Count == 0)
            throw new ValidationException("The submitted guest sessions do not contain any dishes.");

        Order order = await orderRepository.GetOrderByReservationIdAsync(reservationId)
            ?? throw new ValidationException($"Order for reservation '{reservationId}' was not found.");

        order.Dishes ??= [];

        foreach (SubmittedDishResponse dish in mergedDishes)
        {
            OrderItem? existing = order.Dishes.FirstOrDefault(d => d.DishId == dish.DishId);
            if (existing is not null)
            {
                existing.Quantity += dish.Quantity;
            }
            else
            {
                order.Dishes.Add(new OrderItem
                {
                    DishId = dish.DishId,
                    Quantity = dish.Quantity,
                    State = OrderItemState.Pending,
                });
            }
        }

        order.Status = OrderStatus.SubmittedByCustomer;
        await orderRepository.SaveOrderAsync(order);

        group.Status = GroupReservationStatus.Submitted;
        await groupReservationRepository.SaveGroupReservationAsync(group);

        return mergedDishes;
    }
}
