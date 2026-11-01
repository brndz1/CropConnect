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

    // Hex SHA-256 of a one-time account token.
    public const int TokenHashLength = 64;
    public const int TokenPurposeMaxLength = 30;
    public const int TokenMaxLength = 100;

    public const int EventTypeMaxLength = 100;
    public const int RoutingKeyMaxLength = 100;
    public const int OutboxErrorMaxLength = 1000;
    public const int TraceParentMaxLength = 55;
}
