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

    public bool IsActive { get; private set; }

    public override string EntityName => "farm";

    #endregion

    #region Factory

    public static Farm Create(Guid ownerId, string name, string? description, string location, DateTime now)
    {
        var farm = new Farm
        {
            OwnerId = Guard.NotEmpty(ownerId, "Owner"),
            Name = Guard.Required(name, "Name", FieldLimits.NameMaxLength),
            Description = Guard.Optional(description, "Description", FieldLimits.DescriptionMaxLength),
            Location = Guard.Required(location, "Location", FieldLimits.LocationMaxLength),
            IsActive = true
        };

        farm.Initialize(now);
        return farm;
    }

    #endregion

    #region Behaviour

    public void Update(string? name, string? description, string? location, DateTime now)
    {
        var newName = name is null ? Name : Guard.Required(name, "Name", FieldLimits.NameMaxLength);
        var newDescription = description is null ? Description : Guard.Optional(description, "Description", FieldLimits.DescriptionMaxLength);
        var newLocation = location is null ? Location : Guard.Required(location, "Location", FieldLimits.LocationMaxLength);

        Touch(now);
        Name = newName;
        Description = newDescription;
        Location = newLocation;
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
