using APIHandler.Entities;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.Dishes;
using APIHandler.Services.Notifications;
using APIHandler.Services.Orders;
using AutoFixture;
using Moq;

namespace APIHandler.Tests.Services;

public class OrderServiceCancelForReservationTests
{
    private readonly Fixture _fixture = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly OrderService _service;

    public OrderServiceCancelForReservationTests()
    {
        _service = new OrderService(
            _orderRepository.Object,
            new Mock<IReservationRepository>().Object,
            new Mock<IDishService>().Object,
            new Mock<INotificationService>().Object);
    }

    [Theory]
    [InlineData(OrderStatus.Created)]
    [InlineData(OrderStatus.SubmittedByCustomer)]
    [InlineData(OrderStatus.Submitted)]
    public async Task CancelOrderForReservationAsync_ActiveOrder_MarksOrderCancelled(OrderStatus status)
    {
        Order order = _fixture.Build<Order>()
            .With(o => o.ReservationId, "reservation-1")
            .With(o => o.Status, status)
            .Create();
        _orderRepository.Setup(r => r.GetOrderByReservationIdAsync("reservation-1")).ReturnsAsync(order);

        await _service.CancelOrderForReservationAsync("reservation-1");

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        _orderRepository.Verify(r => r.SaveOrderAsync(order), Times.Once);
    }

    [Fact]
    public async Task CancelOrderForReservationAsync_NoOrder_DoesNothing()
    {
        _orderRepository.Setup(r => r.GetOrderByReservationIdAsync("reservation-1")).ReturnsAsync((Order?)null);

        await _service.CancelOrderForReservationAsync("reservation-1");

        _orderRepository.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
    }
}
