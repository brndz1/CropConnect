using Auth.Service.Protos;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Users.Service.Protos;
using CropConnect.ApiGateway.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CropConnect.ApiGateway.Endpoints;

// RF01-RF03: registration, login, the account emails' links and the administrator's user management (Auth.Service).
public static class AuthEndpoints
{
    #region Contracts

    public sealed record RegisterBody(string Username, string Email, string Password, string Role, string FirstName, string LastName);

    public sealed record RegisteredResponse(string UserId);

    public sealed record LoginBody(string Email, string Password);

    // The JWT (valid for ExpiresIn seconds) and the refresh token that gets the next one (see /api/auth/refresh).
    public sealed record TokenResponse(string Token, string RefreshToken, int ExpiresIn);

    public sealed record RefreshTokenBody(string RefreshToken);

    public sealed record UserAccountResponse(string Id, string Username, string Email, string Role, bool IsActive, bool EmailVerified);

    public sealed record TokenBody(string Token);

    public sealed record EmailBody(string Email);

    public sealed record ResetPasswordBody(string Token, string NewPassword);

    public sealed record ChangePasswordBody(string CurrentPassword, string NewPassword);

    public sealed record CreateUserBody(string Username, string Email, string Password, string Role);

    public sealed record UpdateUserBody(string? Username, string? Email, string? Role);

    public sealed record SetActiveBody(bool IsActive);

    #endregion

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var auth = routes.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous();

        auth.MapPost("/register", async Task<Results<Created<RegisteredResponse>, ValidationProblem>> (
                RegisterBody body,
                AuthService.AuthServiceClient client,
                UsersService.UsersServiceClient users,
                IOptions<DownstreamOptions> downstream,
                ILogger<RegisterBody> logger,
                CancellationToken ct) =>
            {
                // Checked first, so an account is never created without the profile it needs.
                var names = new Dictionary<string, string[]>();
                if (!ValidName(body.FirstName)) names["firstName"] = [$"First name is required and must have at most {NameMaxLength} characters."];
                if (!ValidName(body.LastName)) names["lastName"] = [$"Last name is required and must have at most {NameMaxLength} characters."];
                if (names.Count > 0) return TypedResults.ValidationProblem(names);

                var response = await client.RegisterAsync(
                    new RegisterRequest { Username = body.Username ?? "", Email = body.Email ?? "", Password = body.Password ?? "", Role = body.Role ?? "" },
                    cancellationToken: ct);

                // The user cannot log in before confirming the email, so the gateway creates the profile for them.
                // If Users.Service is down the account still exists and the names can be filled in on the profile page.
                try
                {
                    await users.CreateUserProfileAsync(
                        new CreateUserProfileRequest { Id = response.UserId, FirstName = body.FirstName!.Trim(), LastName = body.LastName!.Trim(), PhoneNumber = "" },
                        new Metadata { { "x-api-key", downstream.Value.ApiKey } },
                        cancellationToken: ct);
                }
                catch (RpcException exception)
                {
                    logger.LogWarning(exception, "The profile of new user {UserId} was not created", response.UserId);
                }

                return TypedResults.Created($"/api/users/{response.UserId}", new RegisteredResponse(response.UserId));
            })
            .WithSummary("Register as a client or farm manager, with first and last name (RF01, RF02)");

        auth.MapPost("/login", async Task<Ok<TokenResponse>> (LoginBody body, AuthService.AuthServiceClient client, CancellationToken ct) =>
            {
                var response = await client.LoginAsync(new LoginRequest { Email = body.Email ?? "", Password = body.Password ?? "" }, cancellationToken: ct);
                return TypedResults.Ok(ToTokenResponse(response));
            })
            .RequireLoginRateLimit()
            .WithSummary("Log in and get a JWT for the other endpoints, and a refresh token (RF01); 10 attempts per account every 15 minutes");

        auth.MapPost("/refresh", async Task<Ok<TokenResponse>> (RefreshTokenBody body, AuthService.AuthServiceClient client, CancellationToken ct) =>
            {
                var response = await client.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = body.RefreshToken ?? "" }, cancellationToken: ct);
                return TypedResults.Ok(ToTokenResponse(response));
            })
            .WithSummary("Get a new JWT with the refresh token; the refresh token is replaced and can only be used once");

        auth.MapPost("/logout", async Task<NoContent> (RefreshTokenBody body, AuthService.AuthServiceClient client, CancellationToken ct) =>
            {
                await client.LogoutAsync(new RefreshTokenRequest { RefreshToken = body.RefreshToken ?? "" }, cancellationToken: ct);
                return TypedResults.NoContent();
            })
            .WithSummary("End the session: its refresh token stops working");

        auth.MapPost("/verify-email", async Task<NoContent> (TokenBody body, AuthService.AuthServiceClient client, CancellationToken ct) =>
            {
                await client.VerifyEmailAsync(new TokenRequest { Token = body.Token ?? "" }, cancellationToken: ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Confirm the email address with the token from the verification email");

        // 202 whether or not the email has an account, so these two cannot be used to find out who is registered.
        auth.MapPost("/resend-verification", async Task<Accepted> (EmailBody body, AuthService.AuthServiceClient client, CancellationToken ct) =>
            {
                await client.ResendVerificationAsync(new EmailRequest { Email = body.Email ?? "" }, cancellationToken: ct);
                return TypedResults.Accepted((string?)null);
            })
            .WithSummary("Send the verification email again (at most once a minute)");

        auth.MapPost("/forgot-password", async Task<Accepted> (EmailBody body, AuthService.AuthServiceClient client, CancellationToken ct) =>
            {
                await client.RequestPasswordResetAsync(new EmailRequest { Email = body.Email ?? "" }, cancellationToken: ct);
                return TypedResults.Accepted((string?)null);
            })
            .WithSummary("Email a link to choose a new password (valid for 1 hour)");

        auth.MapPost("/reset-password", async Task<NoContent> (ResetPasswordBody body, AuthService.AuthServiceClient client, CancellationToken ct) =>
            {
                await client.ResetPasswordAsync(
                    new ResetPasswordRequest { Token = body.Token ?? "", NewPassword = body.NewPassword ?? "" },
                    cancellationToken: ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Choose a new password with the token from the reset or invitation email");

        routes.MapPost("/api/auth/change-password", async Task<Ok<TokenResponse>> (ChangePasswordBody body, AuthService.AuthServiceClient client, CancellationToken ct) =>
            {
                var response = await client.ChangePasswordAsync(
                    new ChangePasswordRequest { CurrentPassword = body.CurrentPassword ?? "", NewPassword = body.NewPassword ?? "" },
                    cancellationToken: ct);
                return TypedResults.Ok(ToTokenResponse(response));
            })
            .WithTags("Auth")
            .RequireAuthorization()
            .WithSummary("Change your own password; returns a new token, as the old ones stop working");

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

    private const int NameMaxLength = 100;

    private static bool ValidName(string? name) => name?.Trim() is { Length: > 0 and <= NameMaxLength };

    private static TokenResponse ToTokenResponse(LoginResponse response) =>
        new(response.Token, response.RefreshToken, response.ExpiresIn);

    private static UserAccountResponse ToResponse(UserAccountInfo user) =>
        new(user.Id, user.Username, user.Email, user.Role, user.IsActive, user.EmailVerified);
}
