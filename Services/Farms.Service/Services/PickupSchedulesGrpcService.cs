using Farms.Service.Application;
using Farms.Service.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace Farms.Service.Services;

[Authorize]
public sealed class PickupSchedulesGrpcService : PickupSchedulesService.PickupSchedulesServiceBase
{
    #region Dependencies

    private readonly IPickupScheduleApplicationService _schedules;

    public PickupSchedulesGrpcService(IPickupScheduleApplicationService schedules)
    {
        _schedules = schedules;
    }

    #endregion

    #region Queries

    [AllowAnonymous]
    public override async Task<PickupScheduleInfo> GetPickupSchedule(GetPickupScheduleRequest request, ServerCallContext context)
    {
        var scheduleId = RequestParsing.ParseId(request.Id, "Schedule ID");
        var schedule = await _schedules.GetAsync(context.GetCaller(), scheduleId, context.CancellationToken);
        return schedule.ToProto();
    }

    [AllowAnonymous]
    public override async Task<ListPickupSchedulesResponse> ListPickupSchedules(ListPickupSchedulesRequest request, ServerCallContext context)
    {
        var farmId = RequestParsing.ParseId(request.FarmId, "Farm ID");
        var schedules = await _schedules.ListByFarmAsync(context.GetCaller(), farmId, context.CancellationToken);
        return schedules.ToProto();
    }

    #endregion

    #region Commands

    public override async Task<PickupScheduleInfo> CreatePickupSchedule(CreatePickupScheduleRequest request, ServerCallContext context)
    {
        var command = new CreatePickupScheduleCommand(
            RequestParsing.ParseId(request.FarmId, "Farm ID"),
            request.DayOfWeek.ToDomain(),
            RequestParsing.ParseTime(request.OpenTime, "Open time"),
            RequestParsing.ParseTime(request.CloseTime, "Close time"));

        var schedule = await _schedules.CreateAsync(context.GetCaller(), command, context.CancellationToken);
        return schedule.ToProto();
    }

    public override async Task<PickupScheduleInfo> UpdatePickupSchedule(UpdatePickupScheduleRequest request, ServerCallContext context)
    {
        var command = new UpdatePickupScheduleCommand(
            RequestParsing.ParseId(request.Id, "Schedule ID"),
            request.HasDayOfWeek ? request.DayOfWeek.ToDomain() : null,
            RequestParsing.ParseOptionalTime(request.HasOpenTime, request.OpenTime, "Open time"),
            RequestParsing.ParseOptionalTime(request.HasCloseTime, request.CloseTime, "Close time"),
            RequestParsing.OptionalNumber(request.HasExpectedVersion, request.ExpectedVersion));

        var schedule = await _schedules.UpdateAsync(context.GetCaller(), command, context.CancellationToken);
        return schedule.ToProto();
    }

    public override async Task<Empty> DeletePickupSchedule(DeletePickupScheduleRequest request, ServerCallContext context)
    {
        var scheduleId = RequestParsing.ParseId(request.Id, "Schedule ID");
        await _schedules.DeleteAsync(context.GetCaller(), scheduleId, context.CancellationToken);
        return new Empty();
    }

    #endregion
}
