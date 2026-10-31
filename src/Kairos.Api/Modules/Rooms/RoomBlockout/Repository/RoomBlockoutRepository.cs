using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Repository;

public sealed class RoomBlockoutRepository(AppDbContext context)
    : BaseRepositoryImpl<RoomBlockoutModel>(context),
        IRoomBlockoutRepository
{
    public async Task<IReadOnlyList<RoomBlockoutModel>> GetByRoomIdAsync(
        string roomId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<RoomBlockoutModel>()
            .AsNoTracking()
            .Where(blockout => blockout.RoomId == roomId)
            .OrderBy(blockout => blockout.StartsAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoomBlockoutModel>> GetOverlappingAsync(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<RoomBlockoutModel>()
            .AsNoTracking()
            .Where(blockout =>
                blockout.RoomId == roomId &&
                blockout.StartsAt < endsAt &&
                blockout.EndsAt > startsAt)
            .OrderBy(blockout => blockout.StartsAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsOverlappingAsync(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        CancellationToken cancellationToken = default)
    {
        return context.Set<RoomBlockoutModel>()
            .AnyAsync(
                blockout =>
                    blockout.RoomId == roomId &&
                    blockout.StartsAt < endsAt &&
                    blockout.EndsAt > startsAt,
                cancellationToken);
    }
    
    public async Task<Page<RoomBlockoutModel>> Find(
        FilterRoomBlockoutDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var query = context.Set<RoomBlockoutModel>()
            .AsNoTracking()
            .Include(x => x.Room)
            .Include(x => x.Creater)
            .Include(x => x.Canceller)
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
            query = query.Where(x => x.Version >= dto.VersionMin.Value);
        }

        if (dto.VersionMax.HasValue)
        {
            query = query.Where(x => x.Version <= dto.VersionMax.Value);
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
        // Type and status
        // ---------------------------------------------------------------

        if (dto.Type.HasValue)
        {
            query = query.Where(x =>
                x.Type == dto.Type.Value);
        }

        if (dto.Status.HasValue)
        {
            query = query.Where(x =>
                x.Status == dto.Status.Value);
        }

        // ---------------------------------------------------------------
        // Reason
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Reason))
        {
            string pattern = $"%{dto.Reason.Trim()}%";

            query = dto.ReasonIgnoreCase == true
                ? query.Where(x =>
                    EF.Functions.ILike(x.Reason, pattern))
                : query.Where(x =>
                    EF.Functions.Like(x.Reason, pattern));
        }

        // ---------------------------------------------------------------
        // Notes
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            string pattern = $"%{dto.Notes.Trim()}%";

            query = dto.NotesIgnoreCase == true
                ? query.Where(x =>
                    x.Notes != null &&
                    EF.Functions.ILike(x.Notes, pattern))
                : query.Where(x =>
                    x.Notes != null &&
                    EF.Functions.Like(x.Notes, pattern));
        }

        // ---------------------------------------------------------------
        // Period
        // ---------------------------------------------------------------

        if (dto.StartsAtFrom.HasValue)
        {
            query = query.Where(x =>
                x.StartsAt >= dto.StartsAtFrom.Value);
        }

        if (dto.StartsAtTo.HasValue)
        {
            query = query.Where(x =>
                x.StartsAt <= dto.StartsAtTo.Value);
        }

        if (dto.EndsAtFrom.HasValue)
        {
            query = query.Where(x =>
                x.EndsAt >= dto.EndsAtFrom.Value);
        }

        if (dto.EndsAtTo.HasValue)
        {
            query = query.Where(x =>
                x.EndsAt <= dto.EndsAtTo.Value);
        }

        // ---------------------------------------------------------------
        // Creator
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.CreatedBy))
        {
            query = query.Where(x =>
                x.CreatedBy == dto.CreatedBy.Trim());
        }

        if (!string.IsNullOrWhiteSpace(dto.CreatedUserName))
        {
            string pattern = $"%{dto.CreatedUserName.Trim()}%";

            query = dto.CreatedIgnoreCaseUserName == true
                ? query.Where(x =>
                    x.Creater != null &&
                    EF.Functions.ILike(x.Creater.UserName, pattern))
                : query.Where(x =>
                    x.Creater != null &&
                    EF.Functions.Like(x.Creater.UserName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.CreatedEmail))
        {
            string pattern = $"%{dto.CreatedEmail.Trim()}%";

            query = dto.CreatedIgnoreCaseEmail == true
                ? query.Where(x =>
                    x.Creater != null &&
                    EF.Functions.ILike(x.Creater.Email!, pattern))
                : query.Where(x =>
                    x.Creater != null &&
                    EF.Functions.Like(x.Creater.Email!, pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.CreatedFullName))
        {
            string pattern = $"%{dto.CreatedFullName.Trim()}%";

            query = dto.CreatedIgnoreCaseFullName == true
                ? query.Where(x =>
                    x.Creater != null &&
                    EF.Functions.ILike(x.Creater.FullName!, pattern))
                : query.Where(x =>
                    x.Creater != null &&
                    EF.Functions.Like(x.Creater.FullName!, pattern));
        }

        // ---------------------------------------------------------------
        // Canceller
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.CancelledBy))
        {
            query = query.Where(x =>
                x.CancelledBy == dto.CancelledBy.Trim());
        }

        if (!string.IsNullOrWhiteSpace(dto.CancelledUserName))
        {
            string pattern = $"%{dto.CancelledUserName.Trim()}%";

            query = dto.CancelledIgnoreCaseUserName == true
                ? query.Where(x =>
                    x.Canceller != null &&
                    EF.Functions.ILike(x.Canceller.UserName, pattern))
                : query.Where(x =>
                    x.Canceller != null &&
                    EF.Functions.Like(x.Canceller.UserName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.CancelledEmail))
        {
            string pattern = $"%{dto.CancelledEmail.Trim()}%";

            query = dto.CancelledIgnoreCaseEmail == true
                ? query.Where(x =>
                    x.Canceller != null &&
                    EF.Functions.ILike(x.Canceller.Email!, pattern))
                : query.Where(x =>
                    x.Canceller != null &&
                    EF.Functions.Like(x.Canceller.Email!, pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.CancelledFullName))
        {
            string pattern = $"%{dto.CancelledFullName.Trim()}%";

            query = dto.CancelledIgnoreCaseFullName == true
                ? query.Where(x =>
                    x.Canceller != null &&
                    EF.Functions.ILike(x.Canceller.FullName!, pattern))
                : query.Where(x =>
                    x.Canceller != null &&
                    EF.Functions.Like(x.Canceller.FullName!, pattern));
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
                .OrderByDescending(x => x.StartsAt)
                .ThenByDescending(x => x.Id)
            : query
                .OrderBy(x => x.StartsAt)
                .ThenBy(x => x.Id);

        long offset = ((long)pageNumber - 1) * pageSize;

        int skip = (int)Math.Min(offset, int.MaxValue);

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return Page<RoomBlockoutModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
}