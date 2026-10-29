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

    public ProductType Type { get; private set; } = ProductType.Product;

    public DateTime? AvailableUntil { get; private set; }

    // RF08: what a basket holds. A surprise basket keeps it from customers until pickup.
    public string? Contents { get; private set; }

    public override string EntityName => "product";

    public bool IsBasket => Type is ProductType.PreDefinedBasket or ProductType.SurpriseBasket;

    public bool HidesContents => Type == ProductType.SurpriseBasket;

    public bool IsExpired(DateTime now) => IsBasket && AvailableUntil.HasValue && AvailableUntil.Value <= now;

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
        ProductType type,
        DateTime? availableUntil,
        DateTime now,
        string? contents = null)
    {
        if (farm.IsDeleted || !farm.IsActive)
        {
            throw new BusinessRuleException("Products can only be added to an active farm.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new InvalidInputException("Invalid product type.");
        }

        var isBasket = type is ProductType.PreDefinedBasket or ProductType.SurpriseBasket;

        if (isBasket)
        {
            if (availableUntil is null)
            {
                throw new InvalidInputException("Available until date is required for seasonal baskets.");
            }

            if (availableUntil.Value <= now)
            {
                throw new InvalidInputException("Available until date must be in the future.");
            }
        }
        else
        {
            if (availableUntil is not null)
            {
                throw new InvalidInputException("Available until date can only be specified for seasonal baskets.");
            }

            if (!string.IsNullOrWhiteSpace(contents))
            {
                throw new InvalidInputException("Contents can only be specified for seasonal baskets.");
            }
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
            Type = type,
            AvailableUntil = availableUntil.HasValue ? DateTime.SpecifyKind(availableUntil.Value, DateTimeKind.Utc) : null,
            Contents = Guard.Optional(contents, "Contents", FieldLimits.DescriptionMaxLength) is { Length: > 0 } text ? text : null,
            IsAvailable = true
        };

        product.Initialize(now);
        return product;
    }

    #endregion

    #region Behaviour

    public void Update(
        string? name,
        string? description,
        string? unit,
        decimal? price,
        bool? isAvailable,
        DateTime? availableUntil,
        DateTime now,
        string? contents = null)
    {
        var newName = name is null ? Name : Guard.Required(name, "Name", FieldLimits.NameMaxLength);
        var newDescription = description is null ? Description : Guard.Optional(description, "Description", FieldLimits.DescriptionMaxLength);
        var newUnit = unit is null ? Unit : Guard.Required(unit, "Unit", FieldLimits.UnitMaxLength);
        var newPrice = price is null ? Price : Guard.Price(price.Value);

        if (availableUntil is not null)
        {
            if (!IsBasket)
            {
                throw new BusinessRuleException("Available until date can only be specified for seasonal baskets.");
            }

            if (availableUntil.Value <= now)
            {
                throw new InvalidInputException("Available until date must be in the future.");
            }
        }

        if (contents is not null && !IsBasket)
        {
            throw new BusinessRuleException("Contents can only be specified for seasonal baskets.");
        }

        Touch(now);
        Name = newName;
        Description = newDescription;
        Unit = newUnit;
        Price = newPrice;
        IsAvailable = isAvailable ?? IsAvailable;

        if (availableUntil.HasValue)
        {
            AvailableUntil = DateTime.SpecifyKind(availableUntil.Value, DateTimeKind.Utc);
        }

        // An empty value clears the contents.
        if (contents is not null)
        {
            Contents = Guard.Optional(contents, "Contents", FieldLimits.DescriptionMaxLength) is { Length: > 0 } text ? text : null;
        }
    }

    public void Delete(DateTime now)
    {
        MarkDeleted(now);
        IsAvailable = false;
    }

    #endregion
}
