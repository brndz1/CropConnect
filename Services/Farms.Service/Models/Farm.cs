namespace Farms.Service.Models;

public class Farm : VersionedEntity
{
    #region Properties

    private Farm()
    {
    }

    public Guid OwnerId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;

    // Optional; the Sensors service uses them to fetch the local weather forecast.
    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public bool IsActive { get; private set; }

    private readonly List<PickupSchedule> _pickupSchedules = [];
    public IReadOnlyCollection<PickupSchedule> PickupSchedules => _pickupSchedules.AsReadOnly();

    public override string EntityName => "farm";

    #endregion

    #region Factory

    public static Farm Create(
        Guid ownerId,
        string name,
        string? description,
        string location,
        DateTime now,
        double? latitude = null,
        double? longitude = null)
    {
        var farm = new Farm
        {
            OwnerId = Guard.NotEmpty(ownerId, "Owner"),
            Name = Guard.Required(name, "Name", FieldLimits.NameMaxLength),
            Description = Guard.Optional(description, "Description", FieldLimits.DescriptionMaxLength),
            Location = Guard.Required(location, "Location", FieldLimits.LocationMaxLength),
            IsActive = true
        };

        farm.SetCoordinates(latitude, longitude);
        farm.Initialize(now);
        return farm;
    }

    #endregion

    #region Behaviour

    public void Update(
        string? name,
        string? description,
        string? location,
        DateTime now,
        double? latitude = null,
        double? longitude = null)
    {
        var newName = name is null ? Name : Guard.Required(name, "Name", FieldLimits.NameMaxLength);
        var newDescription = description is null ? Description : Guard.Optional(description, "Description", FieldLimits.DescriptionMaxLength);
        var newLocation = location is null ? Location : Guard.Required(location, "Location", FieldLimits.LocationMaxLength);

        if (latitude is not null || longitude is not null)
        {
            SetCoordinates(latitude, longitude);
        }

        Touch(now);
        Name = newName;
        Description = newDescription;
        Location = newLocation;
    }

    private void SetCoordinates(double? latitude, double? longitude)
    {
        if (latitude is null != longitude is null)
        {
            throw new InvalidInputException("Latitude and longitude must be given together.");
        }

        Latitude = latitude is null ? null : Guard.Coordinate(latitude.Value, "Latitude", 90);
        Longitude = longitude is null ? null : Guard.Coordinate(longitude.Value, "Longitude", 180);
    }

    public void SetActive(bool isActive, DateTime now)
    {
        Touch(now);
        IsActive = isActive;
    }

    public void Delete(DateTime now)
    {
        MarkDeleted(now);
        IsActive = false;
    }

    #endregion
}
