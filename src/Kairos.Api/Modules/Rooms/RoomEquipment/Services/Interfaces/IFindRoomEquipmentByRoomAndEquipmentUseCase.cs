using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

public interface IFindRoomEquipmentByRoomAndEquipmentUseCase
{
    Task<Result<RoomEquipmentModel>> ExecuteAsync(
        string roomId,
        string equipmentId,
        CancellationToken ct = default);
}