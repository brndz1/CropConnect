using Farms.Service.Models;

namespace Farms.Service.Tests.Models;

public sealed class FarmAndScheduleTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Farm NewFarm(double? latitude = null, double? longitude = null) =>
        Farm.Create(Guid.NewGuid(), "Quinta do Vale", null, "Braga", Now, latitude, longitude);

    [Fact]
    public void Farm_needs_an_owner_name_and_location()
    {
        Assert.Throws<InvalidInputException>(() => Farm.Create(Guid.Empty, "Quinta", null, "Braga", Now));
        Assert.Throws<InvalidInputException>(() => Farm.Create(Guid.NewGuid(), " ", null, "Braga", Now));
        Assert.Throws<InvalidInputException>(() => Farm.Create(Guid.NewGuid(), "Quinta", null, "", Now));
    }

    [Fact]
    public void Coordinates_come_in_pairs_and_within_range()
    {
        Assert.Throws<InvalidInputException>(() => NewFarm(latitude: 41.5));
        Assert.Throws<InvalidInputException>(() => NewFarm(91, 0));
        Assert.Throws<InvalidInputException>(() => NewFarm(0, -181));
        Assert.Throws<InvalidInputException>(() => NewFarm(double.NaN, 0));

        var farm = NewFarm(41.55, -8.42);
        Assert.Equal(41.55, farm.Latitude);
        Assert.Equal(-8.42, farm.Longitude);
    }

    [Fact]
    public void Update_keeps_the_coordinates_unless_both_are_given()
    {
        var farm = NewFarm(41.55, -8.42);

        farm.Update("Quinta Nova", null, null, Now);
        Assert.Equal(41.55, farm.Latitude);

        Assert.Throws<InvalidInputException>(() => farm.Update(null, null, null, Now, latitude: 40));
    }

    [Fact]
    public void Deleting_a_farm_deactivates_it()
    {
        var farm = NewFarm();

        farm.Delete(Now);

        Assert.True(farm.IsDeleted);
        Assert.False(farm.IsActive);
        Assert.Throws<BusinessRuleException>(() => farm.SetActive(true, Now));
    }

    [Fact]
    public void Pickup_window_must_close_after_it_opens()
    {
        var farm = NewFarm();

        Assert.Throws<InvalidInputException>(() =>
            PickupSchedule.Create(farm, DayOfWeek.Saturday, new TimeOnly(12, 0), new TimeOnly(12, 0), Now));
        Assert.Throws<InvalidInputException>(() =>
            PickupSchedule.Create(farm, (DayOfWeek)9, new TimeOnly(9, 0), new TimeOnly(12, 0), Now));

        var schedule = PickupSchedule.Create(farm, DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(12, 0), Now);
        Assert.Throws<InvalidInputException>(() => schedule.Update(null, new TimeOnly(13, 0), null, Now));

        schedule.Update(DayOfWeek.Sunday, null, new TimeOnly(13, 0), Now);
        Assert.Equal(DayOfWeek.Sunday, schedule.DayOfWeek);
        Assert.Equal(new TimeOnly(13, 0), schedule.CloseTime);
    }

    [Fact]
    public void Inactive_farms_take_no_schedules()
    {
        var farm = NewFarm();
        farm.SetActive(false, Now);

        Assert.Throws<BusinessRuleException>(() =>
            PickupSchedule.Create(farm, DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(12, 0), Now));
    }
}
