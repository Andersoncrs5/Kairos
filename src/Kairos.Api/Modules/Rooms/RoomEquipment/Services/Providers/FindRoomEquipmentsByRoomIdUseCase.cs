using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;

public sealed class FindRoomEquipmentsByRoomIdUseCase(
    IUnitOfWork unitOfWork)
    : IFindRoomEquipmentsByRoomIdUseCase
{
    public async Task<Result<IReadOnlyList<RoomEquipmentModel>>> ExecuteAsync(
        string roomId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Result<IReadOnlyList<RoomEquipmentModel>>.ValidationFailure(
                ["Room ID is required."]);
        }

        if (!Guid.TryParse(roomId, out _))
        {
            return Result<IReadOnlyList<RoomEquipmentModel>>.ValidationFailure(
                ["Room ID must be a valid UUID."]);
        }

        roomId = roomId.Trim();

        // ---------------------------------------------------------------
        // Room existence
        // ---------------------------------------------------------------

        bool roomExists = await unitOfWork.RoomRepository
            .ExistsAsync(roomId, ct);

        if (!roomExists)
        {
            return Result<IReadOnlyList<RoomEquipmentModel>>.NotFound(
                "Room doesn't exist.");
        }

        // ---------------------------------------------------------------
        // Find room equipments
        // ---------------------------------------------------------------

        IReadOnlyList<RoomEquipmentModel> roomEquipments =
            await unitOfWork.RoomEquipmentRepository
                .GetByRoomIdAsync(roomId, ct);

        return Result<IReadOnlyList<RoomEquipmentModel>>
            .Success(roomEquipments);
    }
}