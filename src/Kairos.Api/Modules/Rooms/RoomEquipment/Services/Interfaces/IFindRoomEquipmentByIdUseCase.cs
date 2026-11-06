using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

public interface IFindRoomEquipmentByIdUseCase
{
    Task<Result<RoomEquipmentModel>> ExecuteAsync(string id, CancellationToken ct = default);
}