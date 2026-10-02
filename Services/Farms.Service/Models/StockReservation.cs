namespace Farms.Service.Models;

public class StockReservation
{
    #region Properties

    private StockReservation()
    {
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public ReservationStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? ConfirmedAt { get; private set; }

    public DateTime? ReleasedAt { get; private set; }

    public bool HoldsStock => Status is ReservationStatus.Reserved or ReservationStatus.Confirmed;

    #endregion

    #region Factory

    public static StockReservation Create(Guid orderId, Product product, int quantity, DateTime now, TimeSpan timeToLive) => new()
    {
        Id = Guid.NewGuid(),
        OrderId = Guard.NotEmpty(orderId, "Order"),
        ProductId = product.Id,
        Quantity = Guard.Positive(quantity, "Quantity"),
        UnitPrice = product.Price,
        Status = ReservationStatus.Reserved,
        CreatedAt = now,
        ExpiresAt = now + timeToLive
    };

    #endregion

    #region State transitions

    public bool Confirm(DateTime now)
    {
        switch (Status)
        {
            case ReservationStatus.Confirmed:
                return false;
            case ReservationStatus.Reserved when ExpiresAt <= now:
                throw new BusinessRuleException("The reservation for this order has expired.");
            case ReservationStatus.Reserved:
                Status = ReservationStatus.Confirmed;
                ConfirmedAt = now;
                return true;
            default:
                throw new BusinessRuleException("The reservation for this order is no longer active.");
        }
    }

    public bool Release(DateTime now)
    {
        if (!HoldsStock)
        {
            return false;
        }

        Status = ReservationStatus.Released;
        ReleasedAt = now;
        return true;
    }

    public bool Expire(DateTime now)
    {
        if (Status != ReservationStatus.Reserved || ExpiresAt > now)
        {
            return false;
        }

        Status = ReservationStatus.Expired;
        ReleasedAt = now;
        return true;
    }

    #endregion
}
