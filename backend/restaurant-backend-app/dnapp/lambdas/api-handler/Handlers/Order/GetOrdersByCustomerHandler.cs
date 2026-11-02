using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Helpers;
using APIHandler.Mappers;
using APIHandler.Services.Dishes;
using APIHandler.Services.Orders;
using APIHandler.Services.Reservations;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Handlers.Order;

public class GetOrdersByCustomerHandler : IApiRequestHandler
{
    private readonly IDishService _dishService;
    private readonly IOrderService _orderService;
    private readonly IReservationService _reservationService;

    public GetOrdersByCustomerHandler(IDishService dishService, IOrderService orderService, IReservationService reservationService)
    {
        _dishService = dishService ?? throw new ArgumentNullException(nameof(dishService));
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _reservationService = reservationService ?? throw new ArgumentNullException(nameof(reservationService));
    }

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (request.RequestContext.Authorizer?.Claims is null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue("sub", out var userId))
        {
            return ResponseBuilder.CreateErrorResponse(401, "You should be signed in to access orders.", context.Logger);
        }

        var orders = await _orderService.GetOrdersByCustomerIdAsync(userId);
        orders = orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();

        // An order of a cancelled reservation must not stay in the cart (covers orders created before cancellation cascaded).
        var reservations = await Task.WhenAll(orders.Select(o => _reservationService.GetReservationByIdAsync(o.ReservationId)));
        orders = orders
            .Where((o, i) => reservations[i]?.Status != ReservationStatus.Cancelled)
            .ToList();

        var dishIds = orders.SelectMany(o => o.Dishes.Select(d => d.DishId)).Distinct().ToList();
        var dishes = await _dishService.GetDishesByIdsAsync(dishIds);

        var orderResponses = orders.Select(o => OrderMapper.ToOrderResponse(o, dishes)).ToList();
        return ResponseBuilder.CreateSuccessResponse(orderResponses);
    }
}