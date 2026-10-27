using Auth.Service.Data;
using Auth.Service.Models;
using Auth.Service.Protos;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;

namespace Auth.Service.Services;

[Authorize]
public class UserAccountsGrpcService : Protos.UserAccountsService.UserAccountsServiceBase
{
    public const string AdminPolicy = "Admin";

    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly AuthDbContext _dbContext;
    private readonly ILogger<UserAccountsGrpcService> _logger;

    public UserAccountsGrpcService(AuthDbContext dbContext, ILogger<UserAccountsGrpcService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [Authorize(Policy = AdminPolicy)]
    public override async Task<UserAccountInfo> CreateUser(CreateUserRequest request, ServerCallContext context)
    {
        var adminId = await RequireActiveCallerAsync(context);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = UserInput.Username(request.Username),
            Email = UserInput.Email(request.Email),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(UserInput.Password(request.Password)),
            Role = UserInput.Role(request.Role)
        };

        await EnsureUniqueAsync(user.Username, user.Email, exceptUserId: null, context.CancellationToken);

        _dbContext.Users.Add(user);
        await SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("User {UserId} created with role {Role} by admin {AdminId}.", user.Id, user.Role, adminId);
        return ToInfo(user);
    }

    public override async Task<UserAccountInfo> GetUser(GetUserRequest request, ServerCallContext context)
    {
        var callerId = await RequireActiveCallerAsync(context);
        var userId = ParseId(request.Id);

        if (userId != callerId && !IsAdmin(context))
        {
            throw new ForbiddenException("You can only view your own account.");
        }

        return ToInfo(await FindAsync(userId, context.CancellationToken));
    }

    [Authorize(Policy = AdminPolicy)]
    public override async Task<ListUsersResponse> ListUsers(ListUsersRequest request, ServerCallContext context)
    {
        await RequireActiveCallerAsync(context);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);

        var users = _dbContext.Users.AsNoTracking();

        if (request.HasRole)
        {
            var role = UserInput.Role(request.Role);
            users = users.Where(u => u.Role == role);
        }

        if (request.HasIsActive)
        {
            users = users.Where(u => u.IsActive == request.IsActive);
        }

        if (request.HasSearch && !string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            users = users.Where(u => EF.Functions.ILike(u.Username, pattern, "\\") || EF.Functions.ILike(u.Email, pattern, "\\"));
        }

        var totalCount = await users.CountAsync(context.CancellationToken);
        var items = await users
            .OrderBy(u => u.Username)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(context.CancellationToken);

        var response = new ListUsersResponse { TotalCount = totalCount, Page = page, PageSize = pageSize };
        response.Users.AddRange(items.Select(ToInfo));
        return response;
    }

    [Authorize(Policy = AdminPolicy)]
    public override async Task<UserAccountInfo> UpdateUser(UpdateUserRequest request, ServerCallContext context)
    {
        var adminId = await RequireActiveCallerAsync(context);
        var user = await FindAsync(ParseId(request.Id), context.CancellationToken);

        var username = request.HasUsername ? UserInput.Username(request.Username) : user.Username;
        var email = request.HasEmail ? UserInput.Email(request.Email) : user.Email;
        var role = request.HasRole ? UserInput.Role(request.Role) : user.Role;

        if (user.Id == adminId && role != Role.Admin)
        {
            throw new BusinessRuleException("Administrators cannot remove their own administrator role.");
        }

        await EnsureUniqueAsync(username, email, user.Id, context.CancellationToken);

        user.Username = username;
        user.Email = email;
        user.Role = role;
        await SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("User {UserId} updated by admin {AdminId}.", user.Id, adminId);
        return ToInfo(user);
    }

    [Authorize(Policy = AdminPolicy)]
    public override async Task<UserAccountInfo> SetUserActive(SetUserActiveRequest request, ServerCallContext context)
    {
        var adminId = await RequireActiveCallerAsync(context);
        var user = await FindAsync(ParseId(request.Id), context.CancellationToken);

        if (user.Id == adminId && !request.IsActive)
        {
            throw new BusinessRuleException("Administrators cannot deactivate their own account.");
        }

        user.IsActive = request.IsActive;
        await SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("User {UserId} set to IsActive={IsActive} by admin {AdminId}.", user.Id, user.IsActive, adminId);
        return ToInfo(user);
    }

    // The token outlives a deactivation, so the caller's account is checked on every call.
    private async Task<Guid> RequireActiveCallerAsync(ServerCallContext context)
    {
        var subject = context.GetHttpContext().User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(subject, out var callerId)
            || !await _dbContext.Users.AnyAsync(u => u.Id == callerId && u.IsActive, context.CancellationToken))
        {
            throw new UnauthenticatedException();
        }

        return callerId;
    }

    private static bool IsAdmin(ServerCallContext context) =>
        context.GetHttpContext().User.HasClaim(JwtTokenGenerator.RoleClaim, nameof(Role.Admin));

    private static Guid ParseId(string value) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : throw new InvalidInputException("User ID is not valid.");

    private async Task<User> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
        ?? throw new NotFoundException("User not found.");

    private async Task EnsureUniqueAsync(string username, string email, Guid? exceptUserId, CancellationToken cancellationToken)
    {
        if (await _dbContext.Users.AnyAsync(u => u.Id != exceptUserId && (u.Email == email || u.Username == username), cancellationToken))
        {
            throw new ConflictException("Email or Username already exists.");
        }
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Email or Username already exists.");
        }
    }

    private static UserAccountInfo ToInfo(User user) => new()
    {
        Id = user.Id.ToString(),
        Username = user.Username,
        Email = user.Email,
        Role = user.Role.ToString(),
        IsActive = user.IsActive
    };
}
