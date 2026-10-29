using CropConnect.ApiGateway.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Orders.Service.Protos;

namespace CropConnect.ApiGateway.Endpoints;

// RF10-RF15: orders, their life cycle and reviews (Orders.Service).
public static class OrderEndpoints
{
    #region Contracts

    public enum OrderStatus
    {
        Pending,
        Confirmed,
        ReadyForPickup,
        PickedUp,
        Cancelled
    }

    public sealed record OrderItemResponse(string ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal TotalPrice);

    public sealed record OrderResponse(
        string Id,
        string CustomerId,
        string FarmId,
        OrderStatus? Status,
        decimal TotalAmount,
        IReadOnlyList<OrderItemResponse> Items,
        string? Notes,
        string? CancellationReason,
        DateTime? CreatedAt,
        DateTime? UpdatedAt,
        int Version);

    public sealed record OrderItemBody(string ProductId, int Quantity);

    public sealed record CreateOrderBody(IReadOnlyList<OrderItemBody> Items, string? Notes);

    public sealed record UpdateStatusBody(OrderStatus Status, int? ExpectedVersion);

    public sealed record CancelOrderBody(string? Reason, int? ExpectedVersion);

    public sealed record ReviewResponse(string Id, string OrderId, string CustomerId, string FarmId, int Rating, string Comment, DateTime? CreatedAt);

    public sealed record CreateReviewBody(int Rating, string? Comment);

    #endregion

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder routes)
    {
        MapOrders(routes.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization());
        MapReviews(routes);
        return routes;
    }

    #region Orders

    private static void MapOrders(RouteGroupBuilder orders)
    {
        orders.MapPost("/", async Task<Created<OrderResponse>> (CreateOrderBody body, OrdersService.OrdersServiceClient client, CancellationToken ct) =>
            {
                var request = new CreateOrderRequest();
                request.Items.AddRange((body.Items ?? []).Select(i => new OrderItemInput { ProductId = i.ProductId ?? "", Quantity = i.Quantity }));
                if (body.Notes is not null) request.Notes = body.Notes;

                var created = await client.CreateOrderAsync(request, cancellationToken: ct);
                return TypedResults.Created($"/api/orders/{created.Id}", ToResponse(created));
            })
            .WithSummary("Place an order, reserving its stock (client, RF10, RF11)");

        orders.MapGet("/mine", async Task<Ok<PagedResponse<OrderResponse>>> (int? page, int? pageSize, OrderStatus? status, OrdersService.OrdersServiceClient client, CancellationToken ct) =>
            {
                var request = new ListMyOrdersRequest { Page = page ?? 1, PageSize = pageSize ?? 0 };
                if (status is not null) request.Status = ToProto(status.Value);

                var response = await client.ListMyOrdersAsync(request, cancellationToken: ct);
                return TypedResults.Ok(ToPage(response));
            })
            .WithSummary("Your order history and status (client, RF13)");

        orders.MapGet("/", async Task<Ok<PagedResponse<OrderResponse>>> (int? page, int? pageSize, string? farmId, OrderStatus? status, OrdersService.OrdersServiceClient client, CancellationToken ct) =>
            {
                var request = new ListOrdersRequest { Page = page ?? 1, PageSize = pageSize ?? 0 };
                if (farmId is not null) request.FarmId = farmId;
                if (status is not null) request.Status = ToProto(status.Value);

                var response = await client.ListOrdersAsync(request, cancellationToken: ct);
                return TypedResults.Ok(ToPage(response));
            })
            .WithSummary("Orders received by your farms (manager, RF12) or by any farm (administrator)");

        orders.MapGet("/{id}", async Task<Ok<OrderResponse>> (string id, OrdersService.OrdersServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.GetOrderAsync(new GetOrderRequest { Id = id }, cancellationToken: ct))))
            .WithSummary("Get an order you placed or received");

        orders.MapPut("/{id}/status", async Task<Ok<OrderResponse>> (string id, UpdateStatusBody body, OrdersService.OrdersServiceClient client, CancellationToken ct) =>
            {
                var request = new UpdateOrderStatusRequest { Id = id, NewStatus = ToProto(body.Status) };
                if (body.ExpectedVersion is not null) request.ExpectedVersion = body.ExpectedVersion.Value;
                return TypedResults.Ok(ToResponse(await client.UpdateOrderStatusAsync(request, cancellationToken: ct)));
            })
            .WithSummary("Move an order to Confirmed, ReadyForPickup or PickedUp (manager, RF12)");

        orders.MapPost("/{id}/cancel", async Task<Ok<OrderResponse>> (string id, CancelOrderBody? body, OrdersService.OrdersServiceClient client, CancellationToken ct) =>
            {
                var request = new CancelOrderRequest { Id = id };
                if (body?.Reason is not null) request.Reason = body.Reason;
                if (body?.ExpectedVersion is not null) request.ExpectedVersion = body.ExpectedVersion.Value;
                return TypedResults.Ok(ToResponse(await client.CancelOrderAsync(request, cancellationToken: ct)));
            })
            .WithSummary("Cancel an order and release its stock (client before ReadyForPickup, or manager, RF14)");
    }

    #endregion

    #region Reviews (RF15)

    private static void MapReviews(IEndpointRouteBuilder routes)
    {
        var byOrder = routes.MapGroup("/api/orders/{orderId}/review").WithTags("Reviews").RequireAuthorization();

        byOrder.MapPost("/", async Task<Created<ReviewResponse>> (string orderId, CreateReviewBody body, ReviewsService.ReviewsServiceClient client, CancellationToken ct) =>
            {
                var created = await client.CreateReviewAsync(
                    new CreateReviewRequest { OrderId = orderId, Rating = body.Rating, Comment = body.Comment ?? "" },
                    cancellationToken: ct);
                return TypedResults.Created($"/api/orders/{orderId}/review", ToResponse(created));
            })
            .WithSummary("Review a picked-up order (client, RF15)");

        byOrder.MapGet("/", async Task<Ok<ReviewResponse>> (string orderId, ReviewsService.ReviewsServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.GetReviewByOrderAsync(new GetReviewByOrderRequest { OrderId = orderId }, cancellationToken: ct))))
            .WithSummary("Get the review of an order");

        routes.MapGet("/api/farms/{farmId}/reviews", async Task<Ok<PagedResponse<ReviewResponse>>> (
                string farmId, int? page, int? pageSize, ReviewsService.ReviewsServiceClient client, CancellationToken ct) =>
            {
                var request = new ListFarmReviewsRequest { FarmId = farmId, Page = page ?? 1, PageSize = pageSize ?? 0 };
                var response = await client.ListFarmReviewsAsync(request, cancellationToken: ct);
                return TypedResults.Ok(new PagedResponse<ReviewResponse>(
                    response.Reviews.Select(ToResponse).ToList(), response.TotalCount, request.Page, request.PageSize));
            })
            .WithTags("Reviews")
            .AllowAnonymous()
            .WithSummary("A farm's reviews (RF15)");
    }

    #endregion

    #region Mapping

    private static PagedResponse<OrderResponse> ToPage(ListOrdersResponse response) =>
        new(response.Orders.Select(ToResponse).ToList(), response.TotalCount, response.Page, response.PageSize);

    private static OrderResponse ToResponse(OrderInfo order) => new(
        order.Id,
        order.CustomerId,
        order.FarmId,
        FromProto(order.Status),
        order.TotalAmount.ToDecimal(),
        order.Items.Select(i => new OrderItemResponse(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice.ToDecimal(), i.TotalPrice.ToDecimal())).ToList(),
        order.HasNotes ? order.Notes : null,
        order.HasCancellationReason ? order.CancellationReason : null,
        order.CreatedAt.AsDateTime(),
        order.UpdatedAt.AsDateTime(),
        order.Version);

    private static ReviewResponse ToResponse(ReviewInfo review) =>
        new(review.Id, review.OrderId, review.CustomerId, review.FarmId, review.Rating, review.Comment, review.CreatedAt.AsDateTime());

    private static OrderStatusProto ToProto(OrderStatus status) => status switch
    {
        OrderStatus.Pending => OrderStatusProto.OrderStatusPending,
        OrderStatus.Confirmed => OrderStatusProto.OrderStatusConfirmed,
        OrderStatus.ReadyForPickup => OrderStatusProto.OrderStatusReadyForPickup,
        OrderStatus.PickedUp => OrderStatusProto.OrderStatusPickedUp,
        OrderStatus.Cancelled => OrderStatusProto.OrderStatusCancelled,
        _ => OrderStatusProto.OrderStatusUnspecified
    };

    private static OrderStatus? FromProto(OrderStatusProto status) => status switch
    {
        OrderStatusProto.OrderStatusPending => OrderStatus.Pending,
        OrderStatusProto.OrderStatusConfirmed => OrderStatus.Confirmed,
        OrderStatusProto.OrderStatusReadyForPickup => OrderStatus.ReadyForPickup,
        OrderStatusProto.OrderStatusPickedUp => OrderStatus.PickedUp,
        OrderStatusProto.OrderStatusCancelled => OrderStatus.Cancelled,
        _ => null
    };

    #endregion
}
