using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Providers;

public sealed class FindRoomOperatingHoursByRoomIdUseCase(
    IUnitOfWork unitOfWork)
    : IFindRoomOperatingHoursByRoomIdUseCase
{
    public async Task<Result<IReadOnlyList<RoomOperatingHourModel>>> ExecuteAsync(
        string roomId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Result<IReadOnlyList<RoomOperatingHourModel>>.ValidationFailure(
                ["Room ID is required."]);
        }

        roomId = roomId.Trim();

        // ---------------------------------------------------------------
        // Room existence
        // ---------------------------------------------------------------

        bool roomExists = await unitOfWork.RoomRepository
            .ExistsAsync(roomId, ct);

        if (!roomExists)
        {
            return Result<IReadOnlyList<RoomOperatingHourModel>>.NotFound(
                "Room doesn't exist.");
        }

        // ---------------------------------------------------------------
        // Find operating hours
        // ---------------------------------------------------------------

        IReadOnlyList<RoomOperatingHourModel> operatingHours =
            await unitOfWork.RoomOperatingHourRepository
                .GetByRoomIdAsync(roomId, ct);

        return Result<IReadOnlyList<RoomOperatingHourModel>>
            .Success(operatingHours);
    }
}