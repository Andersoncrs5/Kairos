using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.Repositories;

public interface IRoomFavoriteRepository
    : IBaseRepository<RoomFavoriteModel>
{
    Task<RoomFavoriteModel?> GetByUserAndRoomAsync(
        string userId,
        string roomId,
        CancellationToken ct = default);

    Task<bool> ExistsByUserAndRoomAsync(
        string userId,
        string roomId,
        CancellationToken ct = default);

    Task<int> DeleteByUserAndRoomAsync(
        string userId,
        string roomId,
        CancellationToken ct = default);

    Task<int> CountByUserAsync(
        string userId,
        CancellationToken ct = default);

    Task<int> CountByRoomAsync(
        string roomId,
        CancellationToken ct = default);

    Task<IReadOnlySet<string>> GetFavoriteRoomIdsAsync(
        string userId,
        IReadOnlyCollection<string> roomIds,
        CancellationToken ct = default);

    Task<Page<RoomFavoriteModel>> Find(
        FilterRoomFavoriteDto dto,
        CancellationToken ct = default);
}