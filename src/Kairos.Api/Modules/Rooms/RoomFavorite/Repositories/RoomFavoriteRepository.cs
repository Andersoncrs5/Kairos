using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.Repositories;

public sealed class RoomFavoriteRepository(AppDbContext context)
    : BaseRepositoryImpl<RoomFavoriteModel>(context),
        IRoomFavoriteRepository
{
    public Task<RoomFavoriteModel?> GetByUserAndRoomAsync(
        string userId,
        string roomId,
        CancellationToken ct = default)
    {
        return context.Set<RoomFavoriteModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                favorite =>
                    favorite.UserId == userId &&
                    favorite.RoomId == roomId,
                ct);
    }

    public Task<bool> ExistsByUserAndRoomAsync(
        string userId,
        string roomId,
        CancellationToken ct = default)
    {
        return context.Set<RoomFavoriteModel>()
            .AsNoTracking()
            .AnyAsync(
                favorite =>
                    favorite.UserId == userId &&
                    favorite.RoomId == roomId,
                ct);
    }

    public Task<int> DeleteByUserAndRoomAsync(
        string userId,
        string roomId,
        CancellationToken ct = default)
    {
        return context.Set<RoomFavoriteModel>()
            .Where(favorite =>
                favorite.UserId == userId &&
                favorite.RoomId == roomId)
            .ExecuteDeleteAsync(ct);
    }

    public Task<int> CountByUserAsync(
        string userId,
        CancellationToken ct = default)
    {
        return context.Set<RoomFavoriteModel>()
            .AsNoTracking()
            .CountAsync(favorite => favorite.UserId == userId, ct);
    }

    public Task<int> CountByRoomAsync(
        string roomId,
        CancellationToken ct = default)
    {
        return context.Set<RoomFavoriteModel>()
            .AsNoTracking()
            .CountAsync(favorite => favorite.RoomId == roomId, ct);
    }

    public async Task<IReadOnlySet<string>> GetFavoriteRoomIdsAsync(
        string userId,
        IReadOnlyCollection<string> roomIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roomIds);

        if (roomIds.Count == 0)
        {
            return new HashSet<string>();
        }

        List<string> favoriteIds = await context.Set<RoomFavoriteModel>()
            .AsNoTracking()
            .Where(favorite =>
                favorite.UserId == userId &&
                roomIds.Contains(favorite.RoomId))
            .Select(favorite => favorite.RoomId)
            .ToListAsync(ct);

        return favoriteIds.ToHashSet();
    }

    public async Task<Page<RoomFavoriteModel>> Find(
        FilterRoomFavoriteDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        IQueryable<RoomFavoriteModel> query =
            context.Set<RoomFavoriteModel>()
                .AsNoTracking()
                .Include(x => x.Room)
                .Include(x => x.User)
                .ApplyBaseFilter(dto);

        // ---------------------------------------------------------------
        // Identifiers
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.RoomId))
        {
            string roomId = dto.RoomId.Trim();
            query = query.Where(x => x.RoomId == roomId);
        }

        if (!string.IsNullOrWhiteSpace(dto.UserId))
        {
            string userId = dto.UserId.Trim();
            query = query.Where(x => x.UserId == userId);
        }

        // ---------------------------------------------------------------
        // Room
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.CodeRoom))
        {
            string pattern = $"%{dto.CodeRoom.Trim()}%";

            query = dto.CodeRoomIgnoreCase == true
                ? query.Where(x =>
                    x.Room != null &&
                    EF.Functions.ILike(x.Room.Code, pattern))
                : query.Where(x =>
                    x.Room != null &&
                    EF.Functions.Like(x.Room.Code, pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.NameRoom))
        {
            string pattern = $"%{dto.NameRoom.Trim()}%";

            query = dto.NameRoomIgnoreCase == true
                ? query.Where(x =>
                    x.Room != null &&
                    EF.Functions.ILike(x.Room.Name, pattern))
                : query.Where(x =>
                    x.Room != null &&
                    EF.Functions.Like(x.Room.Name, pattern));
        }

        // ---------------------------------------------------------------
        // Total count
        // ---------------------------------------------------------------

        long totalCount = await query.LongCountAsync(ct);

        // ---------------------------------------------------------------
        // Pagination
        // ---------------------------------------------------------------

        int pageNumber = Math.Max(dto.Page ?? 1, 1);
        int pageSize = Math.Clamp(dto.PageSize ?? 20, 1, 100);

        // ---------------------------------------------------------------
        // Deterministic ordering (CreatedAt: o mais recente em DESC)
        // ---------------------------------------------------------------

        query = dto.ColumnOrder == ColumnOrderEnum.DESC
            ? query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
            : query
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id);

        long offset = ((long)pageNumber - 1) * pageSize;

        int skip = (int)Math.Min(offset, int.MaxValue);

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return Page<RoomFavoriteModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
}