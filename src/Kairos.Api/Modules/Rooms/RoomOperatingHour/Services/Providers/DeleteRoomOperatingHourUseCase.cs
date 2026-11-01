using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;

public sealed class DeleteRoomOperatingHourUseCase(
    IUnitOfWork unitOfWork)
    : IDeleteRoomOperatingHourUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string roomOperatingHourId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(roomOperatingHourId))
        {
            return Result<bool>.ValidationFailure(
                ["Room operating hour ID is required."]);
        }

        int deletedCount = await unitOfWork.RoomOperatingHourRepository
            .DeleteByIdAndCountAsync(roomOperatingHourId.Trim(), ct);

        if (deletedCount == 0)
        {
            return Result<bool>.NotFound(
                "Room operating hour not found.");
        }

        return Result<bool>.Success(true);
    }
}