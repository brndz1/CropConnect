namespace Notifications.Service.Models;

public static class Guard
{
    public static string Required(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidInputException($"{field} is required.");
        }

        return Truncate(value.Trim(), maxLength);
    }

    public static Guid NotEmpty(Guid value, string field) =>
        value == Guid.Empty ? throw new InvalidInputException($"{field} is required.") : value;

    // Notification text is built from event data, so it is shortened rather than rejected.
    public static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..(maxLength - 1)] + "…" : value;
}
