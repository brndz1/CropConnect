using Auth.Service.Protos;
using CropConnect.ApiGateway.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CropConnect.ApiGateway.Endpoints;

// RF01-RF03: registration, login and the administrator's user management (Auth.Service).
public static class AuthEndpoints
{
    #region Contracts

    public sealed record RegisterBody(string Username, string Email, string Password, string Role);

    public sealed record RegisteredResponse(string UserId);

    public sealed record LoginBody(string Email, string Password);

    public sealed record TokenResponse(string Token);

    public sealed record UserAccountResponse(string Id, string Username, string Email, string Role, bool IsActive);

    public sealed record CreateUserBody(string Username, string Email, string Password, string Role);

    public sealed record UpdateUserBody(string? Username, string? Email, string? Role);

    public sealed record SetActiveBody(bool IsActive);

    #endregion

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var auth = routes.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous();

        auth.MapPost("/register", async Task<Created<RegisteredResponse>> (RegisterBody body, AuthService.AuthServiceClient client, CancellationToken ct) =>
            {
                var response = await client.RegisterAsync(
                    new RegisterRequest { Username = body.Username ?? "", Email = body.Email ?? "", Password = body.Password ?? "", Role = body.Role ?? "" },
                    cancellationToken: ct);
                return TypedResults.Created($"/api/users/{response.UserId}", new RegisteredResponse(response.UserId));
            })
            .WithSummary("Register as a client or farm manager (RF01, RF02)");

        auth.MapPost("/login", async Task<Ok<TokenResponse>> (LoginBody body, AuthService.AuthServiceClient client, CancellationToken ct) =>
            {
                var response = await client.LoginAsync(new LoginRequest { Email = body.Email ?? "", Password = body.Password ?? "" }, cancellationToken: ct);
                return TypedResults.Ok(new TokenResponse(response.Token));
            })
            .WithSummary("Log in and get a JWT for the other endpoints (RF01)");

        var users = routes.MapGroup("/api/users").WithTags("Users").RequireAuthorization();

        users.MapGet("/", async Task<Ok<PagedResponse<UserAccountResponse>>> (
                int? page, int? pageSize, string? role, bool? isActive, string? search,
                UserAccountsService.UserAccountsServiceClient client, CancellationToken ct) =>
            {
                var request = new ListUsersRequest { Page = page ?? 1, PageSize = pageSize ?? 0 };
                if (role is not null) request.Role = role;
                if (isActive is not null) request.IsActive = isActive.Value;
                if (search is not null) request.Search = search;

                var response = await client.ListUsersAsync(request, cancellationToken: ct);
                return TypedResults.Ok(new PagedResponse<UserAccountResponse>(
                    response.Users.Select(ToResponse).ToList(), response.TotalCount, response.Page, response.PageSize));
            })
            .WithSummary("List user accounts (administrator, RF03)");

        users.MapPost("/", async Task<Created<UserAccountResponse>> (CreateUserBody body, UserAccountsService.UserAccountsServiceClient client, CancellationToken ct) =>
            {
                var created = await client.CreateUserAsync(
                    new CreateUserRequest { Username = body.Username ?? "", Email = body.Email ?? "", Password = body.Password ?? "", Role = body.Role ?? "" },
                    cancellationToken: ct);
                return TypedResults.Created($"/api/users/{created.Id}", ToResponse(created));
            })
            .WithSummary("Create a user account (administrator, RF03)");

        users.MapGet("/{id}", async Task<Ok<UserAccountResponse>> (string id, UserAccountsService.UserAccountsServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.GetUserAsync(new GetUserRequest { Id = id }, cancellationToken: ct))))
            .WithSummary("Get a user account (own account, or any as administrator)");

        users.MapPatch("/{id}", async Task<Ok<UserAccountResponse>> (string id, UpdateUserBody body, UserAccountsService.UserAccountsServiceClient client, CancellationToken ct) =>
            {
                var request = new UpdateUserRequest { Id = id };
                if (body.Username is not null) request.Username = body.Username;
                if (body.Email is not null) request.Email = body.Email;
                if (body.Role is not null) request.Role = body.Role;
                return TypedResults.Ok(ToResponse(await client.UpdateUserAsync(request, cancellationToken: ct)));
            })
            .WithSummary("Edit a user account and its role (administrator, RF03)");

        users.MapPut("/{id}/active", async Task<Ok<UserAccountResponse>> (string id, SetActiveBody body, UserAccountsService.UserAccountsServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.SetUserActiveAsync(new SetUserActiveRequest { Id = id, IsActive = body.IsActive }, cancellationToken: ct))))
            .WithSummary("Activate or deactivate a user account (administrator, RF03)");

        return routes;
    }

    private static UserAccountResponse ToResponse(UserAccountInfo user) =>
        new(user.Id, user.Username, user.Email, user.Role, user.IsActive);
}
