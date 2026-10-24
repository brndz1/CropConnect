namespace Orders.Service.Models;

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    ReadyForPickup = 3,
    PickedUp = 4,
    Cancelled = 5
}
