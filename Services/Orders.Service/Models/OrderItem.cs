namespace Orders.Service.Models;

public sealed class OrderItem
{
    #region Properties

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal TotalPrice => Quantity * UnitPrice;

    public Order? Order { get; private set; }

    #endregion

    #region Factory

    private OrderItem()
    {
    }

    public static OrderItem Create(Guid orderId, Guid productId, string productName, int quantity, decimal unitPrice)
    {
        return new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = Guard.NotEmpty(orderId, nameof(OrderId)),
            ProductId = Guard.NotEmpty(productId, nameof(ProductId)),
            ProductName = Guard.Required(productName, nameof(ProductName), FieldLimits.MaxNameLength),
            Quantity = Guard.Positive(quantity, nameof(Quantity)),
            UnitPrice = Guard.Price(unitPrice)
        };
    }

    #endregion
}
