using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orders.Service.Application.Clients;
using Orders.Service.Application.Events;
using Orders.Service.Data;
using Orders.Service.Models;

namespace Orders.Service.Application;

public sealed class OrderApplicationService : IOrderApplicationService
{
    private readonly OrdersDbContext _dbContext;
    private readonly IFarmStockClient _stockClient;
    private readonly IOrderEventPublisher _eventPublisher;
    private readonly TimeProvider _timeProvider;
    private readonly OrderOptions _options;
    private readonly ILogger<OrderApplicationService> _logger;

    public OrderApplicationService(
        OrdersDbContext dbContext,
        IFarmStockClient stockClient,
        IOrderEventPublisher eventPublisher,
        TimeProvider timeProvider,
        IOptions<OrderOptions> options,
        ILogger<OrderApplicationService> logger)
    {
        _dbContext = dbContext;
        _stockClient = stockClient;
        _eventPublisher = eventPublisher;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    #region Create Order

    public async Task<OrderResult> CreateOrderAsync(
        Caller caller,
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var customerId = caller.RequireUserId();

        if (!caller.IsClient)
        {
            throw new ForbiddenException(ErrorMessages.OnlyClientsCanCreateOrders);
        }

        if (command.Items.Count == 0)
        {
            throw new InvalidInputException(ErrorMessages.OrderItemsRequired);
        }

        if (command.Items.Count > _options.MaxLinesPerOrder)
        {
            throw new InvalidInputException($"An order cannot have more than {_options.MaxLinesPerOrder} lines.");
        }

        var mergedItems = MergeLines(command.Items);
        var orderId = Guid.NewGuid();
        var now = Now;

        var reserved = await _stockClient.ReserveStockAsync(orderId, mergedItems, cancellationToken);

        Order order;
        try
        {
            order = Order.Create(
                orderId,
                customerId,
                reserved.FarmId,
                reserved.FarmOwnerId,
                command.Notes,
                now);

            foreach (var item in reserved.Items)
            {
                var orderItem = OrderItem.Create(
                    orderId,
                    item.ProductId,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice);

                order.AddItem(orderItem);
            }

            // The reservation TTL only covers this creation step; once confirmed, stock stays held
            // until the order is cancelled, however long it waits in Pending.
            await _stockClient.ConfirmStockAsync(orderId, cancellationToken);

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order {OrderId}. Executing stock release compensation.", orderId);
            try
            {
                await _stockClient.ReleaseStockAsync(orderId, CancellationToken.None);
            }
            catch (Exception releaseEx)
            {
                _logger.LogCritical(releaseEx, "Failed to compensate stock release for order {OrderId}!", orderId);
            }

            throw;
        }

        await _eventPublisher.PublishOrderCreatedAsync(
            new OrderCreatedEvent(
                order.Id,
                order.CustomerId,
                order.FarmId,
                order.FarmOwnerId,
                order.TotalAmount,
                now),
            cancellationToken);

        return ToResult(order);
    }

    #endregion

    #region Read Orders

    public async Task<OrderResult> GetOrderByIdAsync(
        Caller caller,
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException(ErrorMessages.OrderNotFound);
        }

        caller.EnsureCanView(order);
        return ToResult(order);
    }

    public async Task<PagedResult<OrderResult>> ListOrdersAsync(
        Caller caller,
        ListOrdersQuery query,
        CancellationToken cancellationToken)
    {
        if (!caller.IsAuthenticated)
        {
            throw new UnauthenticatedException();
        }

        IQueryable<Order> orders = _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items);

        if (caller.IsManager)
        {
            var managerId = caller.RequireUserId();
            orders = orders.Where(o => o.FarmOwnerId == managerId);
        }
        else if (caller.IsAdmin)
        {
            if (query.FarmId.HasValue)
            {
                orders = orders.Where(o => o.FarmId == query.FarmId.Value);
            }
        }
        else
        {
            throw new ForbiddenException("Only managers and administrators can list farm orders.");
        }

        if (query.Status.HasValue)
        {
            orders = orders.Where(o => o.Status == query.Status.Value);
        }

        orders = orders.OrderByDescending(o => o.CreatedAt);

        var paged = await orders.ToPagedResultAsync(query.Page, cancellationToken);
        var mapped = paged.Items.Select(ToResult).ToList();

        return new PagedResult<OrderResult>(mapped, paged.TotalCount, paged.Page, paged.PageSize);
    }

    public async Task<PagedResult<OrderResult>> ListMyOrdersAsync(
        Caller caller,
        ListMyOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var customerId = caller.RequireUserId();

        IQueryable<Order> orders = _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.CustomerId == customerId);

        if (query.Status.HasValue)
        {
            orders = orders.Where(o => o.Status == query.Status.Value);
        }

        orders = orders.OrderByDescending(o => o.CreatedAt);

        var paged = await orders.ToPagedResultAsync(query.Page, cancellationToken);
        var mapped = paged.Items.Select(ToResult).ToList();

        return new PagedResult<OrderResult>(mapped, paged.TotalCount, paged.Page, paged.PageSize);
    }

    #endregion

    #region Update & Cancel Orders

    public async Task<OrderResult> UpdateOrderStatusAsync(
        Caller caller,
        UpdateOrderStatusCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException(ErrorMessages.OrderNotFound);
        }

        caller.EnsureCanManage(order);
        order.EnsureVersion(command.ExpectedVersion);

        var previousStatus = order.Status;
        var now = Now;

        switch (command.NewStatus)
        {
            case OrderStatus.Confirmed:
                order.Confirm(now);
                break;

            case OrderStatus.ReadyForPickup:
                order.MarkReadyForPickup(now);
                break;

            case OrderStatus.PickedUp:
                order.MarkPickedUp(now);
                break;

            case OrderStatus.Cancelled:
                order.Cancel(null, true, now);
                await _stockClient.ReleaseStockAsync(order.Id, cancellationToken);
                break;

            default:
                throw new InvalidInputException($"Unsupported target status '{command.NewStatus}'.");
        }

        await _dbContext.SaveChangesOrConflictAsync(order, cancellationToken);

        if (command.NewStatus == OrderStatus.Cancelled)
        {
            await _eventPublisher.PublishOrderCancelledAsync(
                new OrderCancelledEvent(order.Id, order.CustomerId, order.FarmId, order.FarmOwnerId, order.CancellationReason, now),
                cancellationToken);
        }
        else
        {
            await _eventPublisher.PublishOrderStatusChangedAsync(
                new OrderStatusChangedEvent(order.Id, order.CustomerId, order.FarmId, order.FarmOwnerId, previousStatus, order.Status, now),
                cancellationToken);
        }

        return ToResult(order);
    }

    public async Task<OrderResult> CancelOrderAsync(
        Caller caller,
        CancelOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException(ErrorMessages.OrderNotFound);
        }

        caller.EnsureCanCancel(order);
        order.EnsureVersion(command.ExpectedVersion);

        var isManagerOrAdmin = caller.IsManager || caller.IsAdmin;
        var now = Now;

        order.Cancel(command.Reason, isManagerOrAdmin, now);

        await _dbContext.SaveChangesOrConflictAsync(order, cancellationToken);
        await _stockClient.ReleaseStockAsync(order.Id, cancellationToken);

        await _eventPublisher.PublishOrderCancelledAsync(
            new OrderCancelledEvent(order.Id, order.CustomerId, order.FarmId, order.FarmOwnerId, order.CancellationReason, now),
            cancellationToken);

        return ToResult(order);
    }

    #endregion

    #region Helpers

    private static IReadOnlyList<CreateOrderItemCommand> MergeLines(IEnumerable<CreateOrderItemCommand> items)
    {
        var dictionary = new Dictionary<Guid, int>();

        foreach (var item in items)
        {
            Guard.NotEmpty(item.ProductId, nameof(item.ProductId));
            Guard.Positive(item.Quantity, nameof(item.Quantity));

            dictionary[item.ProductId] = dictionary.TryGetValue(item.ProductId, out var current)
                ? current + item.Quantity
                : item.Quantity;
        }

        return dictionary.Select(kv => new CreateOrderItemCommand(kv.Key, kv.Value)).ToList();
    }

    private static OrderResult ToResult(Order order) => new(
        order.Id,
        order.CustomerId,
        order.FarmId,
        order.Status,
        order.TotalAmount,
        order.Items.Select(i => new OrderItemResult(
            i.ProductId,
            i.ProductName,
            i.Quantity,
            i.UnitPrice,
            i.TotalPrice)).ToList(),
        order.CreatedAt,
        order.UpdatedAt,
        order.Version,
        order.CancellationReason,
        order.Notes);

    #endregion
}
