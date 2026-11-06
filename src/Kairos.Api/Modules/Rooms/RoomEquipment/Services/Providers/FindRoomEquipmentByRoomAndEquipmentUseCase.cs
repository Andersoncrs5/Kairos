using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;

public sealed class FindRoomEquipmentByRoomAndEquipmentUseCase(
    IUnitOfWork unitOfWork)
    : IFindRoomEquipmentByRoomAndEquipmentUseCase
{
    public async Task<Result<RoomEquipmentModel>> ExecuteAsync(
        string roomId,
        string equipmentId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(roomId))
            return Result<RoomEquipmentModel>.ValidationFailure(["Room ID is required."]);

        if (!Guid.TryParse(roomId, out _))
            return Result<RoomEquipmentModel>.ValidationFailure(["Room ID must be a valid UUID."]);

        if (string.IsNullOrWhiteSpace(equipmentId))
            return Result<RoomEquipmentModel>.ValidationFailure(["Equipment ID is required."]);

        if (!Guid.TryParse(equipmentId, out _))
            return Result<RoomEquipmentModel>.ValidationFailure(["Equipment ID must be a valid UUID."]);
        
        // ---------------------------------------------------------------
        // Find room equipment association
        // ---------------------------------------------------------------

        RoomEquipmentModel? roomEquipment =
            await unitOfWork.RoomEquipmentRepository
                .GetByRoomAndEquipmentAsync(roomId, equipmentId, ct);

        if (roomEquipment is null)
        {
            return Result<RoomEquipmentModel>.NotFound(
                "Room equipment association doesn't exist.");
        }

        return Result<RoomEquipmentModel>.Success(roomEquipment);
    }
}