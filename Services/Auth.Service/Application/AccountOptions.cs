using System.ComponentModel.DataAnnotations;

namespace Auth.Service.Application;

public sealed class AccountOptions
{
    public const string SectionName = "Accounts";

    // Self-registered users log in only after opening the link in the verification email.
    public bool RequireVerifiedEmail { get; set; } = true;

    [Range(typeof(TimeSpan), "00:05:00", "7.00:00:00")]
    public TimeSpan EmailVerificationLifetime { get; set; } = TimeSpan.FromHours(24);

    [Range(typeof(TimeSpan), "00:05:00", "1.00:00:00")]
    public TimeSpan PasswordResetLifetime { get; set; } = TimeSpan.FromHours(1);

    [Range(typeof(TimeSpan), "01:00:00", "14.00:00:00")]
    public TimeSpan InvitationLifetime { get; set; } = TimeSpan.FromDays(3);

    // A new verification or reset email for the same account is not sent sooner than this, so the endpoints
    // cannot be used to flood someone's inbox.
    [Range(typeof(TimeSpan), "00:00:00", "01:00:00")]
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromMinutes(1);

    // How long a login session lasts without being used; each refresh starts the period again.
    [Range(typeof(TimeSpan), "01:00:00", "30.00:00:00")]
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
}
