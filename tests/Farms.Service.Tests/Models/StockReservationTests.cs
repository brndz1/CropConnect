using Farms.Service.Models;

namespace Farms.Service.Tests.Models;

public sealed class StockReservationTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(15);

    private static StockReservation NewReservation()
    {
        var farm = Farm.Create(Guid.NewGuid(), "Quinta", null, "Braga", Now);
        var product = Product.Create(farm, "Batata", null, null, 1.20m, 10, ProductType.Product, null, Now);
        return StockReservation.Create(Guid.NewGuid(), product, 3, Now, TimeToLive);
    }

    [Fact]
    public void Reservation_holds_stock_at_the_product_price_until_it_expires()
    {
        var reservation = NewReservation();

        Assert.Equal(ReservationStatus.Reserved, reservation.Status);
        Assert.Equal(1.20m, reservation.UnitPrice);
        Assert.Equal(Now + TimeToLive, reservation.ExpiresAt);
        Assert.True(reservation.HoldsStock);
    }

    [Fact]
    public void Confirming_is_idempotent()
    {
        var reservation = NewReservation();

        Assert.True(reservation.Confirm(Now));
        Assert.False(reservation.Confirm(Now));
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public void Expired_reservation_cannot_be_confirmed()
    {
        var reservation = NewReservation();

        Assert.Throws<BusinessRuleException>(() => reservation.Confirm(Now + TimeToLive));
    }

    [Fact]
    public void Released_reservation_cannot_be_confirmed_or_released_again()
    {
        var reservation = NewReservation();

        Assert.True(reservation.Release(Now));
        Assert.False(reservation.Release(Now));
        Assert.False(reservation.HoldsStock);
        Assert.Throws<BusinessRuleException>(() => reservation.Confirm(Now));
    }

    [Fact]
    public void Confirmed_reservation_never_expires_but_can_be_released()
    {
        var reservation = NewReservation();
        reservation.Confirm(Now);

        Assert.False(reservation.Expire(Now.AddDays(1)));
        Assert.True(reservation.Release(Now.AddDays(1)));
    }

    [Fact]
    public void Reservation_expires_only_once_its_time_is_up()
    {
        var reservation = NewReservation();

        Assert.False(reservation.Expire(Now.AddMinutes(14)));
        Assert.True(reservation.Expire(Now + TimeToLive));
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
    }

    [Fact]
    public void Quantity_must_be_positive()
    {
        var farm = Farm.Create(Guid.NewGuid(), "Quinta", null, "Braga", Now);
        var product = Product.Create(farm, "Batata", null, null, 1m, 10, ProductType.Product, null, Now);

        Assert.Throws<InvalidInputException>(() => StockReservation.Create(Guid.NewGuid(), product, 0, Now, TimeToLive));
        Assert.Throws<InvalidInputException>(() => StockReservation.Create(Guid.Empty, product, 1, Now, TimeToLive));
    }
}
