using Grpc.Core;
using Microsoft.Extensions.Options;
using Orders.Service.Models;
using Farms.Service.Protos;

namespace Orders.Service.Application.Clients;

public sealed class FarmStockClient : IFarmStockClient
{
    private readonly StockService.StockServiceClient _client;
    private readonly FarmStockClientOptions _options;
    private readonly ILogger<FarmStockClient> _logger;

    public FarmStockClient(
        StockService.StockServiceClient client,
        IOptions<FarmStockClientOptions> options,
        ILogger<FarmStockClient> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    private Metadata CreateHeaders() => new()
    {
        { "x-api-key", _options.ApiKey }
    };

    public async Task<ReservedStockResult> ReserveStockAsync(
        Guid orderId,
        IReadOnlyList<CreateOrderItemCommand> items,
        CancellationToken cancellationToken)
    {
        var request = new ReserveStockRequest
        {
            OrderId = orderId.ToString()
        };

        foreach (var item in items)
        {
            request.Items.Add(new StockItem
            {
                ProductId = item.ProductId.ToString(),
                Quantity = item.Quantity
            });
        }

        try
        {
            var response = await _client.ReserveStockAsync(
                request,
                headers: CreateHeaders(),
                cancellationToken: cancellationToken);

            if (response.Items.Count == 0)
            {
                throw new BusinessRuleException("The farm service did not return any reserved items.");
            }

            var firstItem = response.Items[0];
            var farmId = Guid.Parse(firstItem.FarmId);
            var farmOwnerId = Guid.Parse(firstItem.FarmOwnerId);

            var reservedItems = response.Items.Select(i => new ReservedStockItemResult(
                Guid.Parse(i.ProductId),
                i.ProductName,
                i.Quantity,
                FromProtoDecimal(i.UnitPrice))).ToList();

            return new ReservedStockResult(orderId, farmId, farmOwnerId, reservedItems);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to reserve stock for Order {OrderId}: {Detail}", orderId, ex.Status.Detail);
            throw MapRpcException(ex);
        }
    }

    public async Task ConfirmStockAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var request = new ConfirmStockRequest
        {
            OrderId = orderId.ToString()
        };

        try
        {
            await _client.ConfirmStockAsync(
                request,
                headers: CreateHeaders(),
                cancellationToken: cancellationToken);
        }
        catch (RpcException ex)
        {
            _logger.LogWarning(ex, "Failed to confirm stock for Order {OrderId}: {Detail}", orderId, ex.Status.Detail);
            throw MapRpcException(ex);
        }
    }

    public async Task ReleaseStockAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var request = new ReleaseStockRequest
        {
            OrderId = orderId.ToString()
        };

        try
        {
            await _client.ReleaseStockAsync(
                request,
                headers: CreateHeaders(),
                cancellationToken: cancellationToken);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "Failed to release stock for Order {OrderId}: {Detail}", orderId, ex.Status.Detail);
            throw MapRpcException(ex);
        }
    }

    private static decimal FromProtoDecimal(DecimalValue value)
    {
        const decimal nanoFactor = 1_000_000_000m;
        return value.Units + value.Nanos / nanoFactor;
    }

    private static Exception MapRpcException(RpcException ex) => ex.StatusCode switch
    {
        StatusCode.NotFound => new NotFoundException(ex.Status.Detail),
        StatusCode.FailedPrecondition => new BusinessRuleException(ex.Status.Detail),
        StatusCode.InvalidArgument => new InvalidInputException(ex.Status.Detail),
        StatusCode.AlreadyExists => new ConflictException(ex.Status.Detail),
        StatusCode.PermissionDenied => new ForbiddenException(ex.Status.Detail),
        StatusCode.Unauthenticated => new UnauthenticatedException(),
        _ => new InvalidOperationException($"Farm stock service communication failure: {ex.Status.Detail}", ex)
    };
}
