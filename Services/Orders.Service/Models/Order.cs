namespace Orders.Service.Models;

public sealed class Order : VersionedEntity
{
    #region Properties

    public override string EntityName => "Order";

    public Guid CustomerId { get; private set; }

    public Guid FarmId { get; private set; }

    public Guid FarmOwnerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal TotalAmount { get; private set; }

    public string? Notes { get; private set; }

    public string? CancellationReason { get; private set; }

    public List<OrderItem> Items { get; private set; } = [];

    public Review? Review { get; private set; }

    #endregion

    #region Factory

    private Order()
    {
    }

    public static Order Create(
        Guid id,
        Guid customerId,
        Guid farmId,
        Guid farmOwnerId,
        string? notes,
        DateTime now)
    {
        var order = new Order
        {
            CustomerId = Guard.NotEmpty(customerId, nameof(CustomerId)),
            FarmId = Guard.NotEmpty(farmId, nameof(FarmId)),
            FarmOwnerId = Guard.NotEmpty(farmOwnerId, nameof(FarmOwnerId)),
            Status = OrderStatus.Pending,
            TotalAmount = 0m,
            Notes = Guard.Optional(notes, nameof(Notes), FieldLimits.MaxNotesLength)
        };

        order.Initialize(now, id);
        return order;
    }

    public void AddItem(OrderItem item)
    {
        if (Status != OrderStatus.Pending)
        {
            throw new BusinessRuleException("Cannot add items to an order that is not in pending status.");
        }

        Items.Add(item);
        TotalAmount = Guard.Price(TotalAmount + item.TotalPrice);
    }

    #endregion

    #region State Transitions

    public void Confirm(DateTime now)
    {
        if (Status != OrderStatus.Pending)
        {
            throw new BusinessRuleException($"Order cannot be confirmed from status '{Status}'. Only pending orders can be confirmed.");
        }

        Status = OrderStatus.Confirmed;
        Touch(now);
    }

    public void MarkReadyForPickup(DateTime now)
    {
        if (Status != OrderStatus.Confirmed)
        {
            throw new BusinessRuleException($"Order cannot be marked ready for pickup from status '{Status}'. Only confirmed orders can be prepared.");
        }

        Status = OrderStatus.ReadyForPickup;
        Touch(now);
    }

    public void MarkPickedUp(DateTime now)
    {
        if (Status != OrderStatus.ReadyForPickup)
        {
            throw new BusinessRuleException($"Order cannot be marked as picked up from status '{Status}'. Only orders ready for pickup can be completed.");
        }

        Status = OrderStatus.PickedUp;
        Touch(now);
    }

    public void Cancel(string? reason, bool isManagerOrAdmin, DateTime now)
    {
        if (Status is OrderStatus.PickedUp or OrderStatus.Cancelled)
        {
            throw new BusinessRuleException($"Orders in status '{Status}' cannot be cancelled.");
        }

        if (Status == OrderStatus.ReadyForPickup && !isManagerOrAdmin)
        {
            throw new BusinessRuleException("Customers cannot cancel an order that is already marked ready for pickup.");
        }

        Status = OrderStatus.Cancelled;
        CancellationReason = Guard.Optional(reason, nameof(CancellationReason), FieldLimits.MaxReasonLength);
        Touch(now);
    }

    #endregion
}
