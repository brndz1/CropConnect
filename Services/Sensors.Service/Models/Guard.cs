namespace Sensors.Service.Models;

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

    public static string? Optional(string? value, string field, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : MaxLength(value.Trim(), field, maxLength);

    private static string MaxLength(string value, string field, int maxLength) =>
        value.Length > maxLength
            ? throw new InvalidInputException($"{field} must have at most {maxLength} characters.")
            : value;

    #endregion

    #region Numbers

    public static double InRange(double value, string field, (double Min, double Max) range)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidInputException($"{field} must be a finite number.");
        }

        if (value < range.Min || value > range.Max)
        {
            throw new InvalidInputException($"{field} must be between {range.Min} and {range.Max}.");
        }

        return value;
    }

    #endregion

    #region Identifiers

    public static Guid NotEmpty(Guid value, string field) =>
        value == Guid.Empty ? throw new InvalidInputException($"{field} is required.") : value;

    #endregion
}
