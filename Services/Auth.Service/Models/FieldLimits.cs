namespace Auth.Service.Models;

public static class FieldLimits
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 50;
    public const int EmailMaxLength = 254;
    public const int PasswordMinLength = 8;

    // BCrypt ignores everything after the first 72 bytes.
    public const int PasswordMaxLength = 72;
    public const int RoleMaxLength = 20;
}
