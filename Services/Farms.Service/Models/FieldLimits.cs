namespace Farms.Service.Models;

public static class FieldLimits
{
    public const int NameMaxLength = 150;
    public const int DescriptionMaxLength = 1000;
    public const int LocationMaxLength = 250;
    public const int UnitMaxLength = 20;
    public const int StatusMaxLength = 20;
    public const int MoneyPrecision = 10;
    public const int MoneyScale = 2;
    public const decimal MaxPrice = 99_999_999.99m;
}
