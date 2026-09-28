using Auth.Service.Data;
using Auth.Service.Models;
using Auth.Service.Protos;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
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
        // Check if user already exists
        if (await _dbContext.Users.AnyAsync(u => u.Email == request.Email || u.Username == request.Username))
        {
            return new RegisterResponse
            {
                Success = false,
                Message = "Email or Username already exists.",
                UserId = string.Empty
            };
        }

        // Parse role (case-insensitive)
        if (!Enum.TryParse<Role>(request.Role, true, out var role))
        {
            return new RegisterResponse
            {
                Success = false,
                Message = "Invalid role specified.",
                UserId = string.Empty
            };
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Create user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = passwordHash,
            Role = role
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {Email} registered successfully.", user.Email);

        return new RegisterResponse
        {
            Success = true,
            Message = "User registered successfully.",
            UserId = user.Id.ToString()
        };
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        // Find user by email
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        
        // Validate password
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for email {Email}.", request.Email);
            return new LoginResponse
            {
                Success = false,
                Message = "Invalid email or password.",
                Token = string.Empty
            };
        }

        // Generate JWT Token
        var token = _jwtTokenGenerator.GenerateToken(user);
        
        _logger.LogInformation("User {Email} logged in successfully.", user.Email);

        return new LoginResponse
        {
            Success = true,
            Message = "Login successful.",
            Token = token
        };
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
