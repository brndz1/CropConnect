using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Users.Service.Data;
using Users.Service.Models;
using Users.Service.Protos;

namespace Users.Service.Services;

[Authorize]
public class UsersGrpcService : Protos.UsersService.UsersServiceBase
{
    private readonly UsersDbContext _dbContext;
    private readonly ILogger<UsersGrpcService> _logger;

    public UsersGrpcService(UsersDbContext dbContext, ILogger<UsersGrpcService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public override async Task<UserProfileResponse> GetUserProfile(GetUserProfileRequest request, ServerCallContext context)
    {
        var userId = ProfileInput.Id(request.Id);
        EnsureCanAccess(context, userId);

        var profile = await FindAsync(userId, context.CancellationToken);

        return ToResponse(profile);
    }

    public override async Task<UserProfileResponse> CreateUserProfile(CreateUserProfileRequest request, ServerCallContext context)
    {
        var userId = ProfileInput.Id(request.Id);
        EnsureCanAccess(context, userId);

        var profile = new UserProfile
        {
            Id = userId,
            FirstName = ProfileInput.Name(request.FirstName, "First name"),
            LastName = ProfileInput.Name(request.LastName, "Last name"),
            PhoneNumber = ProfileInput.PhoneNumber(request.PhoneNumber),
            CreatedAt = DateTime.UtcNow
        };

        if (await _dbContext.UserProfiles.AnyAsync(u => u.Id == userId, context.CancellationToken))
        {
            throw new ConflictException("User profile already exists.");
        }

        _dbContext.UserProfiles.Add(profile);

        try
        {
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("User profile already exists.");
        }

        _logger.LogInformation("User profile created for User ID {UserId}", userId);

        return ToResponse(profile);
    }

    public override async Task<UserProfileResponse> UpdateUserProfile(UpdateUserProfileRequest request, ServerCallContext context)
    {
        var userId = ProfileInput.Id(request.Id);
        EnsureCanAccess(context, userId);

        var profile = await FindAsync(userId, context.CancellationToken);

        profile.FirstName = ProfileInput.Name(request.FirstName, "First name");
        profile.LastName = ProfileInput.Name(request.LastName, "Last name");
        profile.PhoneNumber = ProfileInput.PhoneNumber(request.PhoneNumber);
        profile.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("User profile updated for User ID {UserId}", userId);

        return ToResponse(profile);
    }

    // A profile belongs to the account with the same ID, so only that user or an administrator may use it.
    private static void EnsureCanAccess(ServerCallContext context, Guid userId)
    {
        var user = context.GetHttpContext().User;
        var isOwner = Guid.TryParse(user.FindFirst("sub")?.Value, out var callerId) && callerId == userId;

        if (!isOwner && !user.HasClaim("role", "Admin"))
        {
            throw new ForbiddenException("You can only access your own profile.");
        }
    }

    private async Task<UserProfile> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        await _dbContext.UserProfiles.FindAsync([userId], cancellationToken)
        ?? throw new NotFoundException("User profile not found.");

    private static UserProfileResponse ToResponse(UserProfile profile) => new()
    {
        Id = profile.Id.ToString(),
        FirstName = profile.FirstName,
        LastName = profile.LastName,
        PhoneNumber = profile.PhoneNumber
    };
}
