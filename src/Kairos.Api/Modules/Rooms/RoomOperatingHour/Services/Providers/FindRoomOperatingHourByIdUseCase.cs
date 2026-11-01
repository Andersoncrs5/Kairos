using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;

public sealed class FindRoomOperatingHourByIdUseCase(
    IUnitOfWork unitOfWork)
    : IFindRoomOperatingHourByIdUseCase
{
    public async Task<Result<RoomOperatingHourModel>> ExecuteAsync(
        string roomOperatingHourId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(roomOperatingHourId))
        {
            return Result<RoomOperatingHourModel>.ValidationFailure(
                ["Room operating hour ID is required."]);
        }

        RoomOperatingHourModel? model =
            await unitOfWork.RoomOperatingHourRepository
                .GetByIdAsync(roomOperatingHourId.Trim(), ct);

        if (model is null)
        {
            return Result<RoomOperatingHourModel>.NotFound(
                "Room operating hour not found.");
        }

        return Result<RoomOperatingHourModel>.Success(model);
    }
}