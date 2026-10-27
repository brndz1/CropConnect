using System.Net.Mail;

namespace Auth.Service.Models;

public static class UserInput
{
    public static string Username(string value)
    {
        var username = value.Trim();

        if (username.Length is < FieldLimits.UsernameMinLength or > FieldLimits.UsernameMaxLength)
        {
            throw new InvalidInputException(
                $"Username must have between {FieldLimits.UsernameMinLength} and {FieldLimits.UsernameMaxLength} characters.");
        }

        return username;
    }

    public static string Email(string value)
    {
        var email = NormalizeEmail(value);

        if (email.Length > FieldLimits.EmailMaxLength
            || !MailAddress.TryCreate(email, out var address)
            || address.Address != email)
        {
            throw new InvalidInputException("Email is not valid.");
        }

        return email;
    }

    public static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();

    public static string Password(string value)
    {
        if (value.Length < FieldLimits.PasswordMinLength)
        {
            throw new InvalidInputException($"Password must have at least {FieldLimits.PasswordMinLength} characters.");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(value) > FieldLimits.PasswordMaxLength)
        {
            throw new InvalidInputException($"Password must have at most {FieldLimits.PasswordMaxLength} bytes.");
        }

        return value;
    }

    public static Role Role(string value) =>
        Enum.TryParse<Role>(value, ignoreCase: true, out var role) && Enum.IsDefined(role)
            ? role
            : throw new InvalidInputException("Invalid role specified.");
}
