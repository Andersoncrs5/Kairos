using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Repository;

public interface IRoomBlockoutRepository
    : IBaseRepository<RoomBlockoutModel>
{
    Task<IReadOnlyList<RoomBlockoutModel>> GetByRoomIdAsync(
        string roomId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomBlockoutModel>> GetOverlappingAsync(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsOverlappingAsync(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        CancellationToken cancellationToken = default);
}