namespace Farms.Service.Models;

public class Product : VersionedEntity
{
    #region Properties

    public const string DefaultUnit = "kg";

    private Product()
    {
    }

    public Guid FarmId { get; private set; }

    public Farm Farm { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Unit { get; private set; } = DefaultUnit;

    public decimal Price { get; private set; }

    public int StockQuantity { get; private set; }

    public bool IsAvailable { get; private set; }

    public override string EntityName => "product";

    public bool IsVisibleInCatalog => !IsDeleted && IsAvailable && Farm is { IsActive: true, IsDeleted: false };

    #endregion

    #region Factory

    public static Product Create(
        Farm farm,
        string name,
        string? description,
        string? unit,
        decimal price,
        int stockQuantity,
        DateTime now)
    {
        if (farm.IsDeleted || !farm.IsActive)
        {
            throw new BusinessRuleException("Products can only be added to an active farm.");
        }

        var product = new Product
        {
            FarmId = farm.Id,
            Farm = farm,
            Name = Guard.Required(name, "Name", FieldLimits.NameMaxLength),
            Description = Guard.Optional(description, "Description", FieldLimits.DescriptionMaxLength),
            Unit = string.IsNullOrWhiteSpace(unit) ? DefaultUnit : Guard.Required(unit, "Unit", FieldLimits.UnitMaxLength),
            Price = Guard.Price(price),
            StockQuantity = Guard.NonNegative(stockQuantity, "Stock quantity"),
            IsAvailable = true
        };

        product.Initialize(now);
        return product;
    }

    #endregion

    #region Behaviour

    public void Update(string? name, string? description, string? unit, decimal? price, bool? isAvailable, DateTime now)
    {
        var newName = name is null ? Name : Guard.Required(name, "Name", FieldLimits.NameMaxLength);
        var newDescription = description is null ? Description : Guard.Optional(description, "Description", FieldLimits.DescriptionMaxLength);
        var newUnit = unit is null ? Unit : Guard.Required(unit, "Unit", FieldLimits.UnitMaxLength);
        var newPrice = price is null ? Price : Guard.Price(price.Value);

        Touch(now);
        Name = newName;
        Description = newDescription;
        Unit = newUnit;
        Price = newPrice;
        IsAvailable = isAvailable ?? IsAvailable;
    }

    public void Delete(DateTime now)
    {
        MarkDeleted(now);
        IsAvailable = false;
    }

    #endregion
}
