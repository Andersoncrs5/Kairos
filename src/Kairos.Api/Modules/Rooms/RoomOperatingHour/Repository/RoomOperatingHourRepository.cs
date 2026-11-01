using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;

public sealed class RoomOperatingHourRepository(AppDbContext context)
    : BaseRepositoryImpl<RoomOperatingHourModel>(context),
        IRoomOperatingHourRepository
{
    public async Task<IReadOnlyList<RoomOperatingHourModel>> GetByRoomIdAsync(
        string roomId,
        CancellationToken ct = default)
    {
        return await context.Set<RoomOperatingHourModel>()
            .AsNoTracking()
            .Where(x => x.RoomId == roomId)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.OpensAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RoomOperatingHourModel>> GetByRoomIdAndDayAsync(
        string roomId,
        DayOfWeek dayOfWeek,
        CancellationToken ct = default)
    {
        return await context.Set<RoomOperatingHourModel>()
            .AsNoTracking()
            .Where(x =>
                x.RoomId == roomId &&
                x.DayOfWeek == dayOfWeek)
            .OrderBy(x => x.OpensAt)
            .ToListAsync(ct);
    }

    public async Task<Page<RoomOperatingHourModel>> Find(
        FilterRoomOperatingHourDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var query = context.Set<RoomOperatingHourModel>()
            .AsNoTracking()
            .Include(x => x.Room)
            .AsQueryable();

        // ---------------------------------------------------------------
        // Identificadores e dados base
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Id))
        {
            query = query.Where(x => x.Id == dto.Id);
        }

        if (dto.VersionMin.HasValue)
        {
            query = query.Where(x =>
                x.Version >= dto.VersionMin.Value);
        }

        if (dto.VersionMax.HasValue)
        {
            query = query.Where(x =>
                x.Version <= dto.VersionMax.Value);
        }

        if (dto.CreatedAtMin.HasValue)
        {
            query = query.Where(x =>
                x.CreatedAt >= dto.CreatedAtMin.Value);
        }

        if (dto.CreatedAtMax.HasValue)
        {
            query = query.Where(x =>
                x.CreatedAt <= dto.CreatedAtMax.Value);
        }

        if (dto.UpdatedAtMin.HasValue)
        {
            query = query.Where(x =>
                x.UpdatedAt.HasValue &&
                x.UpdatedAt.Value >= dto.UpdatedAtMin.Value);
        }

        if (dto.UpdatedAtMax.HasValue)
        {
            query = query.Where(x =>
                x.UpdatedAt.HasValue &&
                x.UpdatedAt.Value <= dto.UpdatedAtMax.Value);
        }

        // ---------------------------------------------------------------
        // Room
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.RoomId))
        {
            query = query.Where(x =>
                x.RoomId == dto.RoomId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(dto.RoomName))
        {
            string pattern = $"%{dto.RoomName.Trim()}%";

            query = dto.RoomNameIgnoreCase == true
                ? query.Where(x =>
                    x.Room != null &&
                    EF.Functions.ILike(x.Room.Name, pattern))
                : query.Where(x =>
                    x.Room != null &&
                    EF.Functions.Like(x.Room.Name, pattern));
        }

        // ---------------------------------------------------------------
        // Day of week
        // ---------------------------------------------------------------

        if (dto.DayOfWeek.HasValue)
        {
            query = query.Where(x =>
                x.DayOfWeek == dto.DayOfWeek.Value);
        }

        // ---------------------------------------------------------------
        // Opening time
        // ---------------------------------------------------------------

        if (dto.OpensAtFrom.HasValue)
        {
            query = query.Where(x =>
                x.OpensAt >= dto.OpensAtFrom.Value);
        }

        if (dto.OpensAtTo.HasValue)
        {
            query = query.Where(x =>
                x.OpensAt <= dto.OpensAtTo.Value);
        }

        // ---------------------------------------------------------------
        // Closing time
        // ---------------------------------------------------------------

        if (dto.ClosesAtFrom.HasValue)
        {
            query = query.Where(x =>
                x.ClosesAt >= dto.ClosesAtFrom.Value);
        }

        if (dto.ClosesAtTo.HasValue)
        {
            query = query.Where(x =>
                x.ClosesAt <= dto.ClosesAtTo.Value);
        }

        // ---------------------------------------------------------------
        // Status
        // ---------------------------------------------------------------

        if (dto.IsClosed.HasValue)
        {
            query = query.Where(x =>
                x.IsClosed == dto.IsClosed.Value);
        }

        if (dto.IsActive.HasValue)
        {
            query = query.Where(x =>
                x.IsActive == dto.IsActive.Value);
        }

        // ---------------------------------------------------------------
        // Total após filtros, antes da paginação
        // ---------------------------------------------------------------

        long totalCount = await query.LongCountAsync(ct);

        // ---------------------------------------------------------------
        // Paginação
        // ---------------------------------------------------------------

        int pageNumber = Math.Max(dto.Page ?? 1, 1);
        int pageSize = Math.Clamp(dto.PageSize ?? 20, 1, 100);

        // ---------------------------------------------------------------
        // Ordenação determinística
        // ---------------------------------------------------------------

        query = dto.ColumnOrder == ColumnOrderEnum.DESC
            ? query
                .OrderByDescending(x => x.DayOfWeek)
                .ThenByDescending(x => x.OpensAt)
                .ThenByDescending(x => x.Id)
            : query
                .OrderBy(x => x.DayOfWeek)
                .ThenBy(x => x.OpensAt)
                .ThenBy(x => x.Id);

        long offset = ((long)pageNumber - 1) * pageSize;

        int skip = (int)Math.Min(offset, int.MaxValue);

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return Page<RoomOperatingHourModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
}