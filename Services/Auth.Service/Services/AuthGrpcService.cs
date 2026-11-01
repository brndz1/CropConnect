using Auth.Service.Application;
using Auth.Service.Data;
using Auth.Service.Messaging;
using Auth.Service.Models;
using Auth.Service.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;

namespace Auth.Service.Services;

public class AuthGrpcService : Protos.AuthService.AuthServiceBase
{
    public const string EmailNotVerifiedMessage =
        "Confirm your email address before logging in. Open the link we sent you or request a new one.";

    public const string SamePasswordMessage = "The new password must be different from the current one.";

    private static readonly Empty Done = new();

    private readonly AuthDbContext _dbContext;
    private readonly JwtTokenGenerator _jwtTokenGenerator;
    private readonly JwtTokenValidator _jwtTokenValidator;
    private readonly AccountTokenService _tokens;
    private readonly ILogger<AuthGrpcService> _logger;

    public AuthGrpcService(
        AuthDbContext dbContext,
        JwtTokenGenerator jwtTokenGenerator,
        JwtTokenValidator jwtTokenValidator,
        AccountTokenService tokens,
        ILogger<AuthGrpcService> logger)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
        _jwtTokenValidator = jwtTokenValidator;
        _tokens = tokens;
        _logger = logger;
    }

    #region Register and login

    public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        var username = UserInput.Username(request.Username);
        var email = UserInput.Email(request.Email);
        var password = UserInput.Password(request.Password);
        var role = UserInput.Role(request.Role);

        if (role == Role.Admin)
        {
            throw new ForbiddenException("Administrators cannot self-register.");
        }

        // Check if user already exists
        if (await _dbContext.Users.AnyAsync(u => u.Email == email || u.Username == username, context.CancellationToken))
        {
            throw new ConflictException("Email or Username already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role
        };

        _dbContext.Users.Add(user);
        await _tokens.IssueAsync(
            user,
            AccountTokenPurpose.EmailVerification,
            _tokens.Options.EmailVerificationLifetime,
            MessagingTopology.EmailVerificationRequestedRoutingKey,
            respectCooldown: false,
            context.CancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another registration with the same email or username won the race.
            throw new ConflictException("Email or Username already exists.");
        }

        _logger.LogInformation("User {Email} registered successfully.", user.Email);

        return new RegisterResponse { UserId = user.Id.ToString() };
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var email = UserInput.NormalizeEmail(request.Email);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, context.CancellationToken);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for email {Email}.", email);
            throw new UnauthenticatedException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for deactivated user {Email}.", email);
            throw new ForbiddenException("This account has been deactivated.");
        }

        // Checked after the password, so it tells nothing about an account to someone who doesn't know it.
        if (!user.IsEmailVerified && _tokens.Options.RequireVerifiedEmail)
        {
            _logger.LogInformation("Login attempt for unverified user {Email}.", email);
            throw new ForbiddenException(EmailNotVerifiedMessage);
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        _logger.LogInformation("User {Email} logged in successfully.", user.Email);

        return new LoginResponse { Token = token };
    }

    public override async Task<ValidateTokenResponse> ValidateToken(ValidateTokenRequest request, ServerCallContext context)
    {
        var principal = _jwtTokenValidator.Validate(request.Token);

        if (principal is null || !Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
        {
            return new ValidateTokenResponse { IsValid = false };
        }

        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, context.CancellationToken);

        // A deactivated user's tokens, and the ones issued before a password change, stop being valid before they expire.
        if (user is not { IsActive: true } || IssuedBeforePasswordChange(principal, user))
        {
            return new ValidateTokenResponse { IsValid = false };
        }

        return new ValidateTokenResponse
        {
            IsValid = true,
            UserId = userId.ToString(),
            Role = principal.FindFirst(JwtTokenGenerator.RoleClaim)?.Value ?? string.Empty
        };
    }

    // iat has a precision of seconds, so a token issued in the same second as the change is kept.
    private static bool IssuedBeforePasswordChange(System.Security.Claims.ClaimsPrincipal principal, User user) =>
        user.PasswordChangedAt is { } changedAt
        && long.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Iat)?.Value, out var issuedAt)
        && issuedAt < new DateTimeOffset(DateTime.SpecifyKind(changedAt, DateTimeKind.Utc)).ToUnixTimeSeconds();

    #endregion

    #region Email verification

    public override async Task<Empty> VerifyEmail(TokenRequest request, ServerCallContext context)
    {
        var (_, user) = await _tokens.RedeemAsync(request.Token, AccountTokenPurpose.EmailVerification, context.CancellationToken);

        if (!user.IsEmailVerified)
        {
            user.EmailVerifiedAt = _tokens.Now;
            AccountEvents.AddEvent(_dbContext, MessagingTopology.EmailVerifiedRoutingKey, user, _tokens.Now);
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);
        _logger.LogInformation("User {UserId} verified their email.", user.Id);
        return Done;
    }

    // Always answers the same way, so it cannot be used to find out which emails have an account.
    public override async Task<Empty> ResendVerification(EmailRequest request, ServerCallContext context)
    {
        var email = UserInput.Email(request.Email);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, context.CancellationToken);

        if (user is { IsActive: true, IsEmailVerified: false }
            && await _tokens.IssueAsync(
                user,
                AccountTokenPurpose.EmailVerification,
                _tokens.Options.EmailVerificationLifetime,
                MessagingTopology.EmailVerificationRequestedRoutingKey,
                respectCooldown: true,
                context.CancellationToken))
        {
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("Verification email requested again for user {UserId}.", user.Id);
        }

        return Done;
    }

    #endregion

    #region Password

    // Always answers the same way, so it cannot be used to find out which emails have an account.
    public override async Task<Empty> RequestPasswordReset(EmailRequest request, ServerCallContext context)
    {
        var email = UserInput.Email(request.Email);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, context.CancellationToken);

        if (user is { IsActive: true }
            && await _tokens.IssueAsync(
                user,
                AccountTokenPurpose.PasswordReset,
                _tokens.Options.PasswordResetLifetime,
                MessagingTopology.PasswordResetRequestedRoutingKey,
                respectCooldown: true,
                context.CancellationToken))
        {
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("Password reset requested for user {UserId}.", user.Id);
        }

        return Done;
    }

    public override async Task<Empty> ResetPassword(ResetPasswordRequest request, ServerCallContext context)
    {
        // Validated first, so a weak password doesn't use up the link.
        var password = UserInput.Password(request.NewPassword);
        var (_, user) = await _tokens.RedeemAsync(request.Token, AccountTokenPurpose.PasswordReset, context.CancellationToken);

        SetPassword(user, password);

        // Opening a link sent to the address proves it belongs to the user.
        user.EmailVerifiedAt ??= _tokens.Now;

        await _dbContext.SaveChangesAsync(context.CancellationToken);
        _logger.LogInformation("User {UserId} reset their password.", user.Id);
        return Done;
    }

    // Returns a new token: the caller's current one stops being valid with the old password.
    [Authorize]
    public override async Task<LoginResponse> ChangePassword(ChangePasswordRequest request, ServerCallContext context)
    {
        var subject = context.GetHttpContext().User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var user = Guid.TryParse(subject, out var userId)
            ? await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, context.CancellationToken)
            : null;

        if (user is null)
        {
            throw new UnauthenticatedException();
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
        {
            throw new InvalidInputException("The current password is not correct.");
        }

        var password = UserInput.Password(request.NewPassword);
        EnsureNewPassword(user, password);

        SetPassword(user, password);
        await _dbContext.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("User {UserId} changed their password.", user.Id);
        return new LoginResponse { Token = _jwtTokenGenerator.GenerateToken(user) };
    }

    private static void EnsureNewPassword(User user, string password)
    {
        if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            throw new InvalidInputException(SamePasswordMessage);
        }
    }

    private void SetPassword(User user, string password)
    {
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        user.PasswordChangedAt = _tokens.Now;
        AccountEvents.AddEvent(_dbContext, MessagingTopology.PasswordChangedRoutingKey, user, _tokens.Now);
    }

    #endregion
}
