using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Rooms.Room.DTOs.Request;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Rooms.Room.Repositories;

public interface IRoomRepository : IBaseRepository<RoomModel>
{
    Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);
    Task<Page<RoomModel>> Find(
        FilterRoomDto dto,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomModel>> GetByRoomTypeIdAsync(
        string roomTypeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomModel>> GetActiveAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomModel>> GetActiveByRoomTypeIdAsync(
        string roomTypeId,
        CancellationToken cancellationToken = default);
}