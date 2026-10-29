using CropConnect.ApiGateway.Infrastructure;
using Farms.Service.Protos;
using Microsoft.AspNetCore.Http.HttpResults;
using ProtoDayOfWeek = Farms.Service.Protos.DayOfWeek;

namespace CropConnect.ApiGateway.Endpoints;

// RF04, RF05, RF07, RF09: farms and their pickup schedules (Farms.Service).
public static class FarmEndpoints
{
    #region Contracts

    public sealed record FarmResponse(
        string Id,
        string OwnerId,
        string Name,
        string Description,
        string Location,
        double? Latitude,
        double? Longitude,
        bool IsActive,
        IReadOnlyList<PickupScheduleResponse> PickupSchedules,
        DateTime? CreatedAt,
        DateTime? UpdatedAt,
        int Version);

    public sealed record CreateFarmBody(string Name, string? Description, string Location, double? Latitude, double? Longitude);

    public sealed record UpdateFarmBody(string? Name, string? Description, string? Location, double? Latitude, double? Longitude, int? ExpectedVersion);

    public sealed record SetActiveBody(bool IsActive);

    public sealed record PickupScheduleResponse(
        string Id,
        string FarmId,
        System.DayOfWeek? DayOfWeek,
        string OpenTime,
        string CloseTime,
        int Version);

    public sealed record CreatePickupScheduleBody(System.DayOfWeek DayOfWeek, string OpenTime, string CloseTime);

    public sealed record UpdatePickupScheduleBody(System.DayOfWeek? DayOfWeek, string? OpenTime, string? CloseTime, int? ExpectedVersion);

    #endregion

    public static IEndpointRouteBuilder MapFarmEndpoints(this IEndpointRouteBuilder routes)
    {
        MapFarms(routes.MapGroup("/api/farms").WithTags("Farms").RequireAuthorization());
        MapPickupSchedules(routes);
        return routes;
    }

    #region Farms

    private static void MapFarms(RouteGroupBuilder farms)
    {
        farms.MapGet("/", async Task<Ok<PagedResponse<FarmResponse>>> (
                int? page, int? pageSize, string? search, string? ownerId, bool? includeInactive,
                FarmsService.FarmsServiceClient client, CancellationToken ct) =>
            {
                var request = new ListFarmsRequest { Page = page ?? 1, PageSize = pageSize ?? 0, IncludeInactive = includeInactive ?? false };
                if (search is not null) request.Search = search;
                if (ownerId is not null) request.OwnerId = ownerId;

                var response = await client.ListFarmsAsync(request, cancellationToken: ct);
                return TypedResults.Ok(new PagedResponse<FarmResponse>(
                    response.Farms.Select(ToResponse).ToList(), response.TotalCount, response.Page, response.PageSize));
            })
            .AllowAnonymous()
            .WithSummary("Browse the active farms (RF09); administrators can include inactive ones (RF04)");

        farms.MapGet("/mine", async Task<Ok<PagedResponse<FarmResponse>>> (int? page, int? pageSize, FarmsService.FarmsServiceClient client, CancellationToken ct) =>
            {
                var response = await client.ListMyFarmsAsync(new ListMyFarmsRequest { Page = page ?? 1, PageSize = pageSize ?? 0 }, cancellationToken: ct);
                return TypedResults.Ok(new PagedResponse<FarmResponse>(
                    response.Farms.Select(ToResponse).ToList(), response.TotalCount, response.Page, response.PageSize));
            })
            .WithSummary("List the farms you manage (RF05)");

        farms.MapGet("/{id}", async Task<Ok<FarmResponse>> (string id, FarmsService.FarmsServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.GetFarmAsync(new GetFarmRequest { Id = id }, cancellationToken: ct))))
            .AllowAnonymous()
            .WithSummary("Get a farm with its pickup schedules (RF09)");

        farms.MapPost("/", async Task<Created<FarmResponse>> (CreateFarmBody body, FarmsService.FarmsServiceClient client, CancellationToken ct) =>
            {
                var request = new CreateFarmRequest { Name = body.Name ?? "", Description = body.Description ?? "", Location = body.Location ?? "" };
                if (body.Latitude is not null) request.Latitude = body.Latitude.Value;
                if (body.Longitude is not null) request.Longitude = body.Longitude.Value;

                var created = await client.CreateFarmAsync(request, cancellationToken: ct);
                return TypedResults.Created($"/api/farms/{created.Id}", ToResponse(created));
            })
            .WithSummary("Register a farm (manager, RF05)");

        farms.MapPatch("/{id}", async Task<Ok<FarmResponse>> (string id, UpdateFarmBody body, FarmsService.FarmsServiceClient client, CancellationToken ct) =>
            {
                var request = new UpdateFarmRequest { Id = id };
                if (body.Name is not null) request.Name = body.Name;
                if (body.Description is not null) request.Description = body.Description;
                if (body.Location is not null) request.Location = body.Location;
                if (body.Latitude is not null) request.Latitude = body.Latitude.Value;
                if (body.Longitude is not null) request.Longitude = body.Longitude.Value;
                if (body.ExpectedVersion is not null) request.ExpectedVersion = body.ExpectedVersion.Value;
                return TypedResults.Ok(ToResponse(await client.UpdateFarmAsync(request, cancellationToken: ct)));
            })
            .WithSummary("Edit your farm (manager, RF05)");

        farms.MapPut("/{id}/active", async Task<Ok<FarmResponse>> (string id, SetActiveBody body, FarmsService.FarmsServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.SetFarmActiveAsync(new SetFarmActiveRequest { Id = id, IsActive = body.IsActive }, cancellationToken: ct))))
            .WithSummary("Activate or deactivate a farm (administrator, RF04)");

        farms.MapDelete("/{id}", async Task<NoContent> (string id, FarmsService.FarmsServiceClient client, CancellationToken ct) =>
            {
                await client.DeleteFarmAsync(new DeleteFarmRequest { Id = id }, cancellationToken: ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Delete your farm (manager, RF05)");
    }

    #endregion

    #region Pickup schedules (RF07)

    private static void MapPickupSchedules(IEndpointRouteBuilder routes)
    {
        var byFarm = routes.MapGroup("/api/farms/{farmId}/pickup-schedules").WithTags("Pickup schedules").RequireAuthorization();

        byFarm.MapGet("/", async Task<Ok<IReadOnlyList<PickupScheduleResponse>>> (string farmId, PickupSchedulesService.PickupSchedulesServiceClient client, CancellationToken ct) =>
            {
                var response = await client.ListPickupSchedulesAsync(new ListPickupSchedulesRequest { FarmId = farmId }, cancellationToken: ct);
                return TypedResults.Ok<IReadOnlyList<PickupScheduleResponse>>(response.Schedules.Select(ToResponse).ToList());
            })
            .AllowAnonymous()
            .WithSummary("List a farm's pickup days and hours (RF09)");

        byFarm.MapPost("/", async Task<Created<PickupScheduleResponse>> (string farmId, CreatePickupScheduleBody body, PickupSchedulesService.PickupSchedulesServiceClient client, CancellationToken ct) =>
            {
                var created = await client.CreatePickupScheduleAsync(
                    new CreatePickupScheduleRequest { FarmId = farmId, DayOfWeek = ToProto(body.DayOfWeek), OpenTime = body.OpenTime ?? "", CloseTime = body.CloseTime ?? "" },
                    cancellationToken: ct);
                return TypedResults.Created($"/api/pickup-schedules/{created.Id}", ToResponse(created));
            })
            .WithSummary("Add a pickup day and hours to your farm (manager, RF07)");

        var schedules = routes.MapGroup("/api/pickup-schedules").WithTags("Pickup schedules").RequireAuthorization();

        schedules.MapGet("/{id}", async Task<Ok<PickupScheduleResponse>> (string id, PickupSchedulesService.PickupSchedulesServiceClient client, CancellationToken ct) =>
                TypedResults.Ok(ToResponse(await client.GetPickupScheduleAsync(new GetPickupScheduleRequest { Id = id }, cancellationToken: ct))))
            .AllowAnonymous()
            .WithSummary("Get a pickup schedule");

        schedules.MapPatch("/{id}", async Task<Ok<PickupScheduleResponse>> (string id, UpdatePickupScheduleBody body, PickupSchedulesService.PickupSchedulesServiceClient client, CancellationToken ct) =>
            {
                var request = new UpdatePickupScheduleRequest { Id = id };
                if (body.DayOfWeek is not null) request.DayOfWeek = ToProto(body.DayOfWeek.Value);
                if (body.OpenTime is not null) request.OpenTime = body.OpenTime;
                if (body.CloseTime is not null) request.CloseTime = body.CloseTime;
                if (body.ExpectedVersion is not null) request.ExpectedVersion = body.ExpectedVersion.Value;
                return TypedResults.Ok(ToResponse(await client.UpdatePickupScheduleAsync(request, cancellationToken: ct)));
            })
            .WithSummary("Edit a pickup schedule (manager, RF07)");

        schedules.MapDelete("/{id}", async Task<NoContent> (string id, PickupSchedulesService.PickupSchedulesServiceClient client, CancellationToken ct) =>
            {
                await client.DeletePickupScheduleAsync(new DeletePickupScheduleRequest { Id = id }, cancellationToken: ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Remove a pickup schedule (manager, RF07)");
    }

    #endregion

    #region Mapping

    private static FarmResponse ToResponse(FarmInfo farm) => new(
        farm.Id,
        farm.OwnerId,
        farm.Name,
        farm.Description,
        farm.Location,
        farm.HasLatitude ? farm.Latitude : null,
        farm.HasLongitude ? farm.Longitude : null,
        farm.IsActive,
        farm.PickupSchedules.Select(ToResponse).ToList(),
        farm.CreatedAt.AsDateTime(),
        farm.UpdatedAt.AsDateTime(),
        farm.Version);

    private static PickupScheduleResponse ToResponse(PickupScheduleInfo schedule) => new(
        schedule.Id,
        schedule.FarmId,
        FromProto(schedule.DayOfWeek),
        schedule.OpenTime,
        schedule.CloseTime,
        schedule.Version);

    // The proto enum starts at 1 for Sunday (0 is "unspecified"); System.DayOfWeek starts at 0.
    private static ProtoDayOfWeek ToProto(System.DayOfWeek day) => (ProtoDayOfWeek)((int)day + 1);

    private static System.DayOfWeek? FromProto(ProtoDayOfWeek day) =>
        day == ProtoDayOfWeek.Unspecified ? null : (System.DayOfWeek)((int)day - 1);

    #endregion
}
