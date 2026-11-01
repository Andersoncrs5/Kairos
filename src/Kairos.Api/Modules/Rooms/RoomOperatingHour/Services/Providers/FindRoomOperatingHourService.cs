using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;

public sealed class FindRoomOperatingHourService(
    IUnitOfWork unitOfWork)
    : IFindRoomOperatingHourService
{
    public async Task<Result<Page<RoomOperatingHourModel>>> ExecuteAsync(
        FilterRoomOperatingHourDto dto,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (dto is null)
        {
            return Result<Page<RoomOperatingHourModel>>.ValidationFailure(
                ["Room operating hour filter is required."]);
        }

        Page<RoomOperatingHourModel> page =
            await unitOfWork.RoomOperatingHourRepository.Find(dto, ct);

        return Result<Page<RoomOperatingHourModel>>.Success(page);
    }
}