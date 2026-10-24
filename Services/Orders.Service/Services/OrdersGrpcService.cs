using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Orders.Service.Application;
using Orders.Service.Protos;

namespace Orders.Service.Services;

[Authorize]
public sealed class OrdersGrpcService : OrdersService.OrdersServiceBase
{
    private readonly IOrderApplicationService _orderService;

    public OrdersGrpcService(IOrderApplicationService orderService)
    {
        _orderService = orderService;
    }

    public override async Task<OrderInfo> CreateOrder(CreateOrderRequest request, ServerCallContext context)
    {
        var caller = context.GetCaller();
        var items = request.Items.Select(i => new CreateOrderItemCommand(
            RequestParsing.ParseId(i.ProductId, nameof(i.ProductId)),
            i.Quantity)).ToList();

        var notes = request.HasNotes ? request.Notes : null;

        var command = new CreateOrderCommand(notes, items);
        var result = await _orderService.CreateOrderAsync(caller, command, context.CancellationToken);

        return result.ToProto();
    }

    public override async Task<OrderInfo> GetOrder(GetOrderRequest request, ServerCallContext context)
    {
        var caller = context.GetCaller();
        var orderId = RequestParsing.ParseId(request.Id, nameof(request.Id));

        var result = await _orderService.GetOrderByIdAsync(caller, orderId, context.CancellationToken);
        return result.ToProto();
    }

    public override async Task<ListOrdersResponse> ListOrders(ListOrdersRequest request, ServerCallContext context)
    {
        var caller = context.GetCaller();
        var page = PageRequest.Create(request.Page, request.PageSize);
        var farmId = RequestParsing.ParseOptionalId(request.HasFarmId, request.FarmId, nameof(request.FarmId));
        var status = request.HasStatus ? request.Status.ToDomain() : null;

        var query = new ListOrdersQuery(page, farmId, status);
        var result = await _orderService.ListOrdersAsync(caller, query, context.CancellationToken);

        var response = new ListOrdersResponse
        {
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        foreach (var order in result.Items)
        {
            response.Orders.Add(order.ToProto());
        }

        return response;
    }

    public override async Task<ListOrdersResponse> ListMyOrders(ListMyOrdersRequest request, ServerCallContext context)
    {
        var caller = context.GetCaller();
        var page = PageRequest.Create(request.Page, request.PageSize);
        var status = request.HasStatus ? request.Status.ToDomain() : null;

        var query = new ListMyOrdersQuery(page, status);
        var result = await _orderService.ListMyOrdersAsync(caller, query, context.CancellationToken);

        var response = new ListOrdersResponse
        {
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        foreach (var order in result.Items)
        {
            response.Orders.Add(order.ToProto());
        }

        return response;
    }

    public override async Task<OrderInfo> UpdateOrderStatus(UpdateOrderStatusRequest request, ServerCallContext context)
    {
        var caller = context.GetCaller();
        var orderId = RequestParsing.ParseId(request.Id, nameof(request.Id));
        var newStatus = request.NewStatus.ToDomainRequired();
        var expectedVersion = RequestParsing.OptionalNumber(request.HasExpectedVersion, request.ExpectedVersion);

        var command = new UpdateOrderStatusCommand(orderId, newStatus, expectedVersion);
        var result = await _orderService.UpdateOrderStatusAsync(caller, command, context.CancellationToken);

        return result.ToProto();
    }

    public override async Task<OrderInfo> CancelOrder(CancelOrderRequest request, ServerCallContext context)
    {
        var caller = context.GetCaller();
        var orderId = RequestParsing.ParseId(request.Id, nameof(request.Id));
        var reason = RequestParsing.OptionalText(request.HasReason, request.Reason);
        var expectedVersion = RequestParsing.OptionalNumber(request.HasExpectedVersion, request.ExpectedVersion);

        var command = new CancelOrderCommand(orderId, reason, expectedVersion);
        var result = await _orderService.CancelOrderAsync(caller, command, context.CancellationToken);

        return result.ToProto();
    }
}
