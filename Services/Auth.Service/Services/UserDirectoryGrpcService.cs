using Auth.Service.Data;
using Auth.Service.Models;
using Auth.Service.Protos;
using Auth.Service.Security;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Auth.Service.Services;

// Only reachable with the internal API key: other services resolve a user ID from an event into
// the contact details they need, without access to the Auth database.
[Authorize(Policy = AuthorizationPolicies.InternalService)]
public sealed class UserDirectoryGrpcService : UserDirectoryService.UserDirectoryServiceBase
{
    private readonly AuthDbContext _dbContext;

    public UserDirectoryGrpcService(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task<UserContactInfo> GetUserContact(GetUserContactRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) || userId == Guid.Empty)
        {
            throw new InvalidInputException("User ID must be a valid identifier.");
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, context.CancellationToken)
            ?? throw new NotFoundException("User not found.");

        return new UserContactInfo
        {
            UserId = user.Id.ToString(),
            Username = user.Username,
            Email = user.Email,
            Role = user.Role.ToString(),
            IsActive = user.IsActive
        };
    }
}
