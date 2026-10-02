namespace Farms.Service.Models;

public static class Guard
{
    #region Text

    public static string Required(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidInputException($"{field} is required.");
        }

        return MaxLength(value.Trim(), field, maxLength);
    }

    public static string Optional(string? value, string field, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : MaxLength(value.Trim(), field, maxLength);

    private static string MaxLength(string value, string field, int maxLength) =>
        value.Length > maxLength
            ? throw new InvalidInputException($"{field} must have at most {maxLength} characters.")
            : value;

    #endregion

    #region Numbers

    public static decimal Price(decimal value)
    {
        if (value < 0)
        {
            throw new InvalidInputException("Price cannot be negative.");
        }

        if (value > FieldLimits.MaxPrice)
        {
            throw new InvalidInputException("Price is too large.");
        }

        if (decimal.Round(value, FieldLimits.MoneyScale) != value)
        {
            throw new InvalidInputException($"Price cannot have more than {FieldLimits.MoneyScale} decimal places.");
        }

        return value;
    }

    public static int NonNegative(int value, string field) =>
        value < 0 ? throw new InvalidInputException($"{field} cannot be negative.") : value;

    public static int Positive(int value, string field) =>
        value <= 0 ? throw new InvalidInputException($"{field} must be greater than zero.") : value;

    #endregion

    #region Identifiers

    public static Guid NotEmpty(Guid value, string field) =>
        value == Guid.Empty ? throw new InvalidInputException($"{field} is required.") : value;

    #endregion
}
