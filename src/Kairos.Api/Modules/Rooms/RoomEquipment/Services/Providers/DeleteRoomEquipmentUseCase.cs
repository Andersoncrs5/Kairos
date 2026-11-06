using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;

public sealed class DeleteRoomEquipmentUseCase(
    IUnitOfWork unitOfWork)
    : IDeleteRoomEquipmentUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string roomEquipmentId,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(roomEquipmentId))
            return Result<bool>.ValidationFailure(["Room equipment ID is required."]);
        
        roomEquipmentId = roomEquipmentId.Trim();

        if (!Guid.TryParse(roomEquipmentId, out _))
            return Result<bool>.ValidationFailure(["Room equipment ID must be a valid UUID."]);
        
        int deletedCount = await unitOfWork.RoomEquipmentRepository
            .DeleteByIdAndCountAsync(roomEquipmentId, ct);

        if (deletedCount == 0)
            return Result<bool>.NotFound("Room equipment not found.");

        return Result<bool>.Success(true);
    }
}