using Auth.Service.Data;
using Auth.Service.Models;
using Auth.Service.Protos;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Auth.Service.Services;

public class AuthGrpcService : Protos.AuthService.AuthServiceBase
{
    private readonly AuthDbContext _dbContext;
    private readonly JwtTokenGenerator _jwtTokenGenerator;
    private readonly ILogger<AuthGrpcService> _logger;

    public AuthGrpcService(AuthDbContext dbContext, JwtTokenGenerator jwtTokenGenerator, ILogger<AuthGrpcService> logger)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
        _logger = logger;
    }

    public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        var username = UserInput.Username(request.Username);
        var email = UserInput.Email(request.Email);
        var password = UserInput.Password(request.Password);
        var role = UserInput.Role(request.Role);

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

        var token = _jwtTokenGenerator.GenerateToken(user);

        _logger.LogInformation("User {Email} logged in successfully.", user.Email);

        return new LoginResponse { Token = token };
    }

    public override Task<ValidateTokenResponse> ValidateToken(ValidateTokenRequest request, ServerCallContext context)
    {
        try
        {
            // TODO LATER: Validate token signature
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(request.Token);
            
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty;
            var role = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? string.Empty;

            return Task.FromResult(new ValidateTokenResponse
            {
                IsValid = true,
                UserId = userId,
                Role = role
            });
        }
        catch
        {
            return Task.FromResult(new ValidateTokenResponse
            {
                IsValid = false,
                UserId = string.Empty,
                Role = string.Empty
            });
        }
    }
}
