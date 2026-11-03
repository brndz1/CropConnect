using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Service.Services;

public class JwtTokenValidator
{
    private readonly IConfiguration _configuration;

    public JwtTokenValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // Same rules as the JwtBearer setup of the other services, so a token is valid here iff they accept it.
    public TokenValidationParameters CreateParameters()
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var publicKey = jwtSettings["PublicKey"] ?? throw new InvalidOperationException("JwtSettings:PublicKey is not configured.");

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtSigningKeys.PublicSecurityKey(publicKey),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }

    public ClaimsPrincipal? Validate(string token)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        try
        {
            return handler.ValidateToken(token, CreateParameters(), out _);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }
}
