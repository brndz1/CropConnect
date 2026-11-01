namespace Auth.Service.Models;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; }
    public bool IsActive { get; set; } = true;

    // Null until the user opens the link in the verification email; accounts an administrator creates start verified.
    public DateTime? EmailVerifiedAt { get; set; }

    // Tokens issued before the last password change stop being valid (see ValidateToken).
    public DateTime? PasswordChangedAt { get; set; }

    public bool IsEmailVerified => EmailVerifiedAt.HasValue;
}
