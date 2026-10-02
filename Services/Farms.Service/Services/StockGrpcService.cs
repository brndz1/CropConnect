using Farms.Service.Application;
using Farms.Service.Protos;
using Farms.Service.Security;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace Farms.Service.Services;

[Authorize(Policy = AuthorizationPolicies.InternalService)]
public sealed class StockGrpcService : StockService.StockServiceBase
{
    #region Dependencies

    private readonly IStockApplicationService _stock;

    public StockGrpcService(IStockApplicationService stock)
    {
        _stock = stock;
    }

    #endregion

    #region Reservation lifecycle

    public override async Task<StockReservationResponse> ReserveStock(ReserveStockRequest request, ServerCallContext context)
    {
        var lines = request.Items
            .Select(item => new StockLine(RequestParsing.ParseId(item.ProductId, "Product ID"), item.Quantity))
            .ToList();

        var command = new ReserveStockCommand(RequestParsing.ParseId(request.OrderId, "Order ID"), lines);
        var result = await _stock.ReserveAsync(command, context.CancellationToken);
        return result.ToProto();
    }

    public override async Task<StockReservationResponse> ConfirmStock(ConfirmStockRequest request, ServerCallContext context)
    {
        var orderId = RequestParsing.ParseId(request.OrderId, "Order ID");
        var result = await _stock.ConfirmAsync(orderId, context.CancellationToken);
        return result.ToProto();
    }

    public override async Task<StockReservationResponse> ReleaseStock(ReleaseStockRequest request, ServerCallContext context)
    {
        var orderId = RequestParsing.ParseId(request.OrderId, "Order ID");
        var result = await _stock.ReleaseAsync(orderId, context.CancellationToken);
        return result.ToProto();
    }

    #endregion
}
