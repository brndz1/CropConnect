using Auth.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Auth.Service.Data;

public static class AdminBootstrapExtensions
{
    public const string SectionName = "BootstrapAdmin";

    // Administrators cannot self-register, so the first one comes from configuration.
    public static async Task SeedBootstrapAdminAsync(this WebApplication app)
    {
        var section = app.Configuration.GetSection(SectionName);
        var email = section["Email"];
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        if (await dbContext.Users.AnyAsync(u => u.Role == Role.Admin))
        {
            return;
        }

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = UserInput.Username(section["Username"] ?? "admin"),
            Email = UserInput.Email(email),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(UserInput.Password(password)),
            Role = Role.Admin
        };

        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync();

        app.Logger.LogInformation("Bootstrap administrator {Email} created.", admin.Email);
    }
}
