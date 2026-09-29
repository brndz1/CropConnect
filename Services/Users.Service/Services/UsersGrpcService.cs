using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Users.Service.Data;
using Users.Service.Models;
using Users.Service.Protos;

namespace Users.Service.Services;

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
        if (!Guid.TryParse(request.Id, out var userId))
        {
            return new UserProfileResponse { Success = false, Message = "Invalid User ID format." };
        }

        var profile = await _dbContext.UserProfiles.FindAsync(userId);

        if (profile == null)
        {
            return new UserProfileResponse { Success = false, Message = "User profile not found." };
        }

        return new UserProfileResponse
        {
            Success = true,
            Message = "Profile retrieved successfully.",
            Id = profile.Id.ToString(),
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            PhoneNumber = profile.PhoneNumber,
            FarmId = profile.FarmId?.ToString() ?? string.Empty
        };
    }

    public override async Task<UserProfileResponse> CreateUserProfile(CreateUserProfileRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var userId))
        {
            return new UserProfileResponse { Success = false, Message = "Invalid User ID format." };
        }

        if (await _dbContext.UserProfiles.AnyAsync(u => u.Id == userId))
        {
            return new UserProfileResponse { Success = false, Message = "User profile already exists." };
        }

        var profile = new UserProfile
        {
            Id = userId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.UserProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User profile created for User ID {UserId}", userId);

        return new UserProfileResponse
        {
            Success = true,
            Message = "Profile created successfully.",
            Id = profile.Id.ToString(),
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            PhoneNumber = profile.PhoneNumber,
            FarmId = string.Empty
        };
    }

    public override async Task<UserProfileResponse> UpdateUserProfile(UpdateUserProfileRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var userId))
        {
            return new UserProfileResponse { Success = false, Message = "Invalid User ID format." };
        }

        var profile = await _dbContext.UserProfiles.FindAsync(userId);

        if (profile == null)
        {
            return new UserProfileResponse { Success = false, Message = "User profile not found." };
        }

        profile.FirstName = request.FirstName;
        profile.LastName = request.LastName;
        profile.PhoneNumber = request.PhoneNumber;
        
        if (!string.IsNullOrEmpty(request.FarmId) && Guid.TryParse(request.FarmId, out var farmId))
        {
            profile.FarmId = farmId;
        }
        
        profile.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User profile updated for User ID {UserId}", userId);

        return new UserProfileResponse
        {
            Success = true,
            Message = "Profile updated successfully.",
            Id = profile.Id.ToString(),
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            PhoneNumber = profile.PhoneNumber,
            FarmId = profile.FarmId?.ToString() ?? string.Empty
        };
    }
}
