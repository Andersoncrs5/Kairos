using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;

public sealed class ExistsRoomOperatingHourUseCase(IUnitOfWork unitOfWork) : IExistsRoomOperatingHourUseCase
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

        bool exists = await unitOfWork.RoomOperatingHourRepository
            .ExistsAsync(roomOperatingHourId.Trim(), ct);

        return Result<bool>.Success(exists);
    }
}