using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Repositories;

public interface IRoomEquipmentRepository : IBaseRepository<RoomEquipmentModel>
{
    Task<Page<RoomEquipmentModel>> Find(
        FilterRoomEquipmentDto dto,
        CancellationToken ct = default);

    Task<RoomEquipmentModel?> GetByRoomAndEquipmentAsync(
        string roomId,
        string equipmentId,
        CancellationToken ct = default);

    Task<bool> ExistsByRoomAndEquipmentAsync(
        string roomId,
        string equipmentId,
        CancellationToken ct = default);

    Task<IReadOnlyList<RoomEquipmentModel>> GetByRoomIdAsync(
        string roomId,
        CancellationToken ct = default);
}