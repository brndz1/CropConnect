namespace Farms.Service.Models;

public class PickupSchedule : VersionedEntity
{
    #region Properties

    private PickupSchedule()
    {
    }

    public Guid FarmId { get; private set; }

    public Farm Farm { get; private set; } = null!;

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly OpenTime { get; private set; }

    public TimeOnly CloseTime { get; private set; }

    public override string EntityName => "pickup schedule";

    #endregion

    #region Factory

    public static PickupSchedule Create(Farm farm, DayOfWeek dayOfWeek, TimeOnly openTime, TimeOnly closeTime, DateTime now)
    {
        if (farm.IsDeleted || !farm.IsActive)
        {
            throw new BusinessRuleException("Pickup schedules can only be added to an active farm.");
        }

        if (!Enum.IsDefined(dayOfWeek))
        {
            throw new InvalidInputException("Invalid day of week.");
        }

        if (closeTime <= openTime)
        {
            throw new InvalidInputException("Close time must be after open time.");
        }

        var schedule = new PickupSchedule
        {
            FarmId = farm.Id,
            Farm = farm,
            DayOfWeek = dayOfWeek,
            OpenTime = openTime,
            CloseTime = closeTime
        };

        schedule.Initialize(now);
        return schedule;
    }

    #endregion

    #region Behaviour

    public void Update(DayOfWeek? dayOfWeek, TimeOnly? openTime, TimeOnly? closeTime, DateTime now)
    {
        var newDayOfWeek = dayOfWeek ?? DayOfWeek;
        var newOpenTime = openTime ?? OpenTime;
        var newCloseTime = closeTime ?? CloseTime;

        if (!Enum.IsDefined(newDayOfWeek))
        {
            throw new InvalidInputException("Invalid day of week.");
        }

        if (newCloseTime <= newOpenTime)
        {
            throw new InvalidInputException("Close time must be after open time.");
        }

        Touch(now);
        DayOfWeek = newDayOfWeek;
        OpenTime = newOpenTime;
        CloseTime = newCloseTime;
    }

    public void Delete(DateTime now)
    {
        MarkDeleted(now);
    }

    #endregion
}
