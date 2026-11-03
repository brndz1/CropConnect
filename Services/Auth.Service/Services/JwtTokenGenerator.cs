using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Auth.Service.Models;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Service.Services;

public class JwtTokenGenerator
{
    // Short claim name written in the token; the other services read the role from it.
    public const string RoleClaim = "role";

    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var privateKey = jwtSettings["PrivateKey"] ?? throw new InvalidOperationException("JwtSettings:PrivateKey is not configured.");
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "60");

        // RS256: only this service has the private key, the others verify the signature with the public one.
        var credentials = new SigningCredentials(JwtSigningKeys.PrivateSecurityKey(privateKey), SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            // Issued-at: ValidateToken rejects tokens issued before the last password change.
            new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(DateTime.UtcNow).ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(RoleClaim, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
