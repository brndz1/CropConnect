using System.Text.RegularExpressions;

namespace Users.Service.Models;

public static partial class ProfileInput
{
    public const int NameMaxLength = 100;
    public const int PhoneNumberMaxLength = 20;

    public static Guid Id(string value) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw new InvalidInputException("Invalid User ID format.");

    public static string Name(string value, string field)
    {
        var name = value.Trim();

        if (name.Length is 0 or > NameMaxLength)
        {
            throw new InvalidInputException($"{field} is required and must have at most {NameMaxLength} characters.");
        }

        return name;
    }

    public static string PhoneNumber(string value)
    {
        var phoneNumber = value.Trim();

        if (phoneNumber.Length > PhoneNumberMaxLength || (phoneNumber.Length > 0 && !PhoneNumberPattern().IsMatch(phoneNumber)))
        {
            throw new InvalidInputException("Phone number is not valid.");
        }

        return phoneNumber;
    }

    [GeneratedRegex(@"^\+?[0-9 ]{6,19}$")]
    private static partial Regex PhoneNumberPattern();
}
