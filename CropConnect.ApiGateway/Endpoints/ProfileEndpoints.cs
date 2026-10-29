using Microsoft.AspNetCore.Http.HttpResults;
using Users.Service.Protos;

namespace CropConnect.ApiGateway.Endpoints;

// Personal details of a user (Users.Service), kept apart from the account in Auth.
public static class ProfileEndpoints
{
    #region Contracts

    public sealed record ProfileBody(string FirstName, string LastName, string PhoneNumber);

    public sealed record ProfileResponse(string Id, string FirstName, string LastName, string PhoneNumber);

    #endregion

    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder routes)
    {
        var profiles = routes.MapGroup("/api/users/{id}/profile").WithTags("Profiles").RequireAuthorization();

        profiles.MapGet("/", async Task<Ok<ProfileResponse>> (string id, UsersService.UsersServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.GetUserProfileAsync(new GetUserProfileRequest { Id = id }, cancellationToken: ct))))
            .WithSummary("Get a user's profile");

        profiles.MapPost("/", async Task<Created<ProfileResponse>> (string id, ProfileBody body, UsersService.UsersServiceClient client, CancellationToken ct) =>
            {
                var created = await client.CreateUserProfileAsync(
                    new CreateUserProfileRequest { Id = id, FirstName = body.FirstName ?? "", LastName = body.LastName ?? "", PhoneNumber = body.PhoneNumber ?? "" },
                    cancellationToken: ct);
                return TypedResults.Created($"/api/users/{id}/profile", ToResponse(created));
            })
            .WithSummary("Create the profile of your own user");

        profiles.MapPut("/", async Task<Ok<ProfileResponse>> (string id, ProfileBody body, UsersService.UsersServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.UpdateUserProfileAsync(
                    new UpdateUserProfileRequest { Id = id, FirstName = body.FirstName ?? "", LastName = body.LastName ?? "", PhoneNumber = body.PhoneNumber ?? "" },
                    cancellationToken: ct))))
            .WithSummary("Replace the profile of your own user");

        return routes;
    }

    private static ProfileResponse ToResponse(UserProfileResponse profile) =>
        new(profile.Id, profile.FirstName, profile.LastName, profile.PhoneNumber);
}
