using Auth.Service.Data;
using Auth.Service.Models;
using Auth.Service.Protos;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;

namespace Auth.Service.Services;

public class AuthGrpcService : Protos.AuthService.AuthServiceBase
{
    private readonly AuthDbContext _dbContext;
    private readonly JwtTokenGenerator _jwtTokenGenerator;
    private readonly JwtTokenValidator _jwtTokenValidator;
    private readonly ILogger<AuthGrpcService> _logger;

    public AuthGrpcService(
        AuthDbContext dbContext,
        JwtTokenGenerator jwtTokenGenerator,
        JwtTokenValidator jwtTokenValidator,
        ILogger<AuthGrpcService> logger)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
        _jwtTokenValidator = jwtTokenValidator;
        _logger = logger;
    }

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

        var token = _jwtTokenGenerator.GenerateToken(user);

        _logger.LogInformation("User {Email} logged in successfully.", user.Email);

        return new LoginResponse { Token = token };
    }

    public override async Task<ValidateTokenResponse> ValidateToken(ValidateTokenRequest request, ServerCallContext context)
    {
        var principal = _jwtTokenValidator.Validate(request.Token);

        // A deactivated user's tokens stop being valid here before they expire.
        if (principal is null
            || !Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
            || !await _dbContext.Users.AnyAsync(u => u.Id == userId && u.IsActive, context.CancellationToken))
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
}
