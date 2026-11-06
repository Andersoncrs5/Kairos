using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

public interface IExistsRoomEquipmentByRoomAndEquipmentUseCase
{
    Task<Result<bool>> ExecuteAsync(
        string roomId,
        string equipmentId,
        CancellationToken ct = default);
}