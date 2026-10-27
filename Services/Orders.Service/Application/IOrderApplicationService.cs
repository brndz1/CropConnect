namespace Orders.Service.Application;

public interface IOrderApplicationService
{
    Task<OrderResult> CreateOrderAsync(Caller caller, CreateOrderCommand command, CancellationToken cancellationToken);

    Task<OrderResult> GetOrderByIdAsync(Caller caller, Guid id, CancellationToken cancellationToken);

    Task<PagedResult<OrderResult>> ListOrdersAsync(Caller caller, ListOrdersQuery query, CancellationToken cancellationToken);

    Task<PagedResult<OrderResult>> ListMyOrdersAsync(Caller caller, ListMyOrdersQuery query, CancellationToken cancellationToken);

    Task<OrderResult> UpdateOrderStatusAsync(Caller caller, UpdateOrderStatusCommand command, CancellationToken cancellationToken);

    Task<OrderResult> CancelOrderAsync(Caller caller, CancelOrderCommand command, CancellationToken cancellationToken);

    Task<int> ReleasePendingStockAsync(CancellationToken cancellationToken);
}
