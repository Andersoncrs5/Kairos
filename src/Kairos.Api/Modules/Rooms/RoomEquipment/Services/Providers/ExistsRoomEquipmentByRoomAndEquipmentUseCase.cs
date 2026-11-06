using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;

public sealed class ExistsRoomEquipmentByRoomAndEquipmentUseCase(
    IUnitOfWork unitOfWork)
    : IExistsRoomEquipmentByRoomAndEquipmentUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string roomId,
        string equipmentId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(roomId))
            return Result<bool>.ValidationFailure(["Room ID is required."]);

        if (!Guid.TryParse(roomId, out _))
            return Result<bool>.ValidationFailure(["Room ID must be a valid UUID."]);

        if (string.IsNullOrWhiteSpace(equipmentId))
            return Result<bool>.ValidationFailure(["Equipment ID is required."]);

        if (!Guid.TryParse(equipmentId, out _))
            return Result<bool>.ValidationFailure(["Equipment ID must be a valid UUID."]);

        bool exists = await unitOfWork.RoomEquipmentRepository
            .ExistsByRoomAndEquipmentAsync(roomId, equipmentId, ct);

        return Result<bool>.Success(exists);
    }
}