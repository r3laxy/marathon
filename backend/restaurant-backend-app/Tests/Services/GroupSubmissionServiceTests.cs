using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.GroupReservations;
using AutoFixture;
using Moq;

namespace APIHandler.Tests.Services;

public class GroupSubmissionServiceTests
{
    private const string OwnerId = "owner-1";

    private readonly Fixture _fixture = new();
    private readonly Mock<IGroupReservationRepository> _groupRepository = new();
    private readonly Mock<IGuestSessionRepository> _sessionRepository = new();
    private readonly Mock<IReservationRepository> _reservationRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly GroupSubmissionService _service;

    private readonly Reservation _reservation;
    private readonly GroupReservation _group;

    public GroupSubmissionServiceTests()
    {
        _service = new GroupSubmissionService(
            _groupRepository.Object,
            _sessionRepository.Object,
            _reservationRepository.Object,
            _orderRepository.Object);

        _reservation = _fixture.Build<Reservation>()
            .With(r => r.Id, "reservation-1")
            .With(r => r.CustomerId, OwnerId)
            .Create();

        _group = _fixture.Build<GroupReservation>()
            .With(g => g.GroupReservationId, "group-1")
            .With(g => g.ReservationId, _reservation.Id)
            .With(g => g.Status, GroupReservationStatus.Open)
            .Create();

        _reservationRepository.Setup(r => r.GetReservationByIdAsync(_reservation.Id)).ReturnsAsync(_reservation);
        _groupRepository.Setup(r => r.GetGroupReservationByIdAsync(_group.GroupReservationId)).ReturnsAsync(_group);
    }

    private GuestSession Session(bool submitted, params (string DishId, int Quantity)[] dishes) =>
        _fixture.Build<GuestSession>()
            .With(s => s.GroupReservationId, _group.GroupReservationId)
            .With(s => s.IsSubmitted, submitted)
            .With(s => s.Dishes, dishes.Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Quantity }).ToList())
            .Create();

    private Order ExistingOrder() =>
        _fixture.Build<Order>()
            .With(o => o.ReservationId, _reservation.Id)
            .With(o => o.Status, OrderStatus.Created)
            .With(o => o.Dishes, new List<OrderItem>())
            .Create();

    [Fact]
    public async Task Submit_MergesDishesOfSubmittedSessionsOnly_AndUpdatesOrderAndGroup()
    {
        var sessions = new List<GuestSession>
        {
            Session(true, ("dish1", 2), ("dish2", 1)),
            Session(true, ("dish1", 1), ("dish2", 1)),
            Session(false, ("dish3", 5)),
        };
        Order order = ExistingOrder();

        _sessionRepository.Setup(r => r.GetAllByGroupReservationIdAsync(_group.GroupReservationId)).ReturnsAsync(sessions);
        _orderRepository.Setup(r => r.GetOrderByReservationIdAsync(_reservation.Id)).ReturnsAsync(order);

        IReadOnlyList<SubmittedDishResponse> result =
            await _service.SubmitGroupPreOrderAsync(OwnerId, _group.GroupReservationId, _reservation.Id);

        Assert.Equal(2, result.Count);
        Assert.Equal(3, result.Single(d => d.DishId == "dish1").Quantity);
        Assert.Equal(2, result.Single(d => d.DishId == "dish2").Quantity);
        Assert.DoesNotContain(result, d => d.DishId == "dish3");

        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(2, order.Dishes.Count);
        Assert.Equal(GroupReservationStatus.Submitted, _group.Status);

        _orderRepository.Verify(r => r.SaveOrderAsync(order), Times.Once);
        _groupRepository.Verify(r => r.SaveGroupReservationAsync(_group), Times.Once);
    }

    [Fact]
    public async Task Submit_NotOwner_ThrowsActionForbidden()
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _service.SubmitGroupPreOrderAsync("someone-else", _group.GroupReservationId, _reservation.Id));

        _orderRepository.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task Submit_ReservationNotFound_ThrowsReservationNotFound()
    {
        _reservationRepository.Setup(r => r.GetReservationByIdAsync("missing")).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            _service.SubmitGroupPreOrderAsync(OwnerId, _group.GroupReservationId, "missing"));
    }

    [Fact]
    public async Task Submit_GroupNotFound_ThrowsGroupReservationNotFound()
    {
        _groupRepository.Setup(r => r.GetGroupReservationByIdAsync("missing")).ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() =>
            _service.SubmitGroupPreOrderAsync(OwnerId, "missing", _reservation.Id));
    }

    [Fact]
    public async Task Submit_NoSubmittedSessions_ThrowsValidationException()
    {
        _sessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(_group.GroupReservationId))
            .ReturnsAsync([Session(false, ("dish1", 1))]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupPreOrderAsync(OwnerId, _group.GroupReservationId, _reservation.Id));
    }

    [Fact]
    public async Task Submit_SubmittedSessionsWithoutDishes_ThrowsValidationException()
    {
        _sessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(_group.GroupReservationId))
            .ReturnsAsync([Session(true)]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupPreOrderAsync(OwnerId, _group.GroupReservationId, _reservation.Id));
    }

    [Fact]
    public async Task Submit_OrderNotFound_ThrowsValidationException()
    {
        _sessionRepository
            .Setup(r => r.GetAllByGroupReservationIdAsync(_group.GroupReservationId))
            .ReturnsAsync([Session(true, ("dish1", 1))]);
        _orderRepository.Setup(r => r.GetOrderByReservationIdAsync(_reservation.Id)).ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupPreOrderAsync(OwnerId, _group.GroupReservationId, _reservation.Id));

        _groupRepository.Verify(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task Submit_GroupAlreadySubmitted_ThrowsGroupReservationSubmitted()
    {
        _group.Status = GroupReservationStatus.Submitted;

        await Assert.ThrowsAsync<GroupReservationSubmittedException>(() =>
            _service.SubmitGroupPreOrderAsync(OwnerId, _group.GroupReservationId, _reservation.Id));
    }
}
