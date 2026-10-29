using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Room.DTOs.Request;
using Kairos.Api.Modules.Rooms.Room.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Rooms.Room.Repositories;

public sealed class RoomRepository(AppDbContext context)
    : BaseRepositoryImpl<RoomModel>(context), IRoomRepository
{
    public Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        return context.Set<RoomModel>()
            .AsNoTracking()
            .AnyAsync(
                room => room.Code == code,
                cancellationToken);
    }
    
    public async Task<Page<RoomModel>> Find(
        FilterRoomDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var query = context.Set<RoomModel>()
            .AsNoTracking()
            .AsQueryable();

        // -----------------------------------------------------------
        // BaseFilter
        // -----------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Id))
        {
            var id = dto.Id.Trim();

            query = query.Where(room => room.Id == id);
        }

        if (dto.VersionMin.HasValue)
        {
            var versionMin = dto.VersionMin.Value;

            query = query.Where(room => room.Version >= versionMin);
        }

        if (dto.VersionMax.HasValue)
        {
            var versionMax = dto.VersionMax.Value;

            query = query.Where(room => room.Version <= versionMax);
        }

        if (dto.CreatedAtMin.HasValue)
        {
            var createdAtMin = dto.CreatedAtMin.Value;

            query = query.Where(
                room => room.CreatedAt >= createdAtMin);
        }

        if (dto.CreatedAtMax.HasValue)
        {
            var createdAtMax = dto.CreatedAtMax.Value;

            query = query.Where(
                room => room.CreatedAt <= createdAtMax);
        }

        if (dto.UpdatedAtMin.HasValue)
        {
            var updatedAtMin = dto.UpdatedAtMin.Value;

            query = query.Where(
                room => room.UpdatedAt.HasValue &&
                        room.UpdatedAt.Value >= updatedAtMin);
        }

        if (dto.UpdatedAtMax.HasValue)
        {
            var updatedAtMax = dto.UpdatedAtMax.Value;

            query = query.Where(
                room => room.UpdatedAt.HasValue &&
                        room.UpdatedAt.Value <= updatedAtMax);
        }

        // -----------------------------------------------------------
        // Room type
        // -----------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.RoomTypeId))
        {
            var roomTypeId = dto.RoomTypeId.Trim();

            query = query.Where(
                room => room.RoomTypeId == roomTypeId);
        }

        if (!string.IsNullOrWhiteSpace(dto.NameRoomType))
        {
            var nameRoomType = dto.NameRoomType.Trim();

            query = dto.NameRoomTypeIgnoreCase == true
                ? query.Where(room =>
                    room.RoomTypeModel != null &&
                    EF.Functions.ILike(
                        room.RoomTypeModel.Name,
                        $"%{nameRoomType}%"))
                : query.Where(room =>
                    room.RoomTypeModel != null &&
                    EF.Functions.Like(
                        room.RoomTypeModel.Name,
                        $"%{nameRoomType}%"));
        }

        // -----------------------------------------------------------
        // Code
        // -----------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var code = dto.Code.Trim();

            query = dto.CodeIgnoreCase == true
                ? query.Where(room =>
                    EF.Functions.ILike(
                        room.Code,
                        $"%{code}%"))
                : query.Where(room =>
                    EF.Functions.Like(
                        room.Code,
                        $"%{code}%"));
        }

        // -----------------------------------------------------------
        // Name
        // -----------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            var name = dto.Name.Trim();

            query = dto.NameIgnoreCase == true
                ? query.Where(room =>
                    EF.Functions.ILike(
                        room.Name,
                        $"%{name}%"))
                : query.Where(room =>
                    EF.Functions.Like(
                        room.Name,
                        $"%{name}%"));
        }

        // -----------------------------------------------------------
        // Description
        // -----------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Description))
        {
            var description = dto.Description.Trim();

            query = dto.DescriptionIgnoreCase == true
                ? query.Where(room =>
                    room.Description != null &&
                    EF.Functions.ILike(
                        room.Description,
                        $"%{description}%"))
                : query.Where(room =>
                    room.Description != null &&
                    EF.Functions.Like(
                        room.Description,
                        $"%{description}%"));
        }

        // -----------------------------------------------------------
        // Location
        // -----------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Location))
        {
            var location = dto.Location.Trim();

            query = dto.LocationIgnoreCase == true
                ? query.Where(room =>
                    EF.Functions.ILike(
                        room.Location,
                        $"%{location}%"))
                : query.Where(room =>
                    EF.Functions.Like(
                        room.Location,
                        $"%{location}%"));
        }

        // -----------------------------------------------------------
        // Floor
        // -----------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Floor))
        {
            var floor = dto.Floor.Trim();

            query = dto.FloorIgnoreCase == true
                ? query.Where(room =>
                    room.Floor != null &&
                    EF.Functions.ILike(
                        room.Floor,
                        $"%{floor}%"))
                : query.Where(room =>
                    room.Floor != null &&
                    EF.Functions.Like(
                        room.Floor,
                        $"%{floor}%"));
        }

        // -----------------------------------------------------------
        // Capacity
        // -----------------------------------------------------------

        if (dto.MinCapacity.HasValue)
        {
            var minCapacity = dto.MinCapacity.Value;

            query = query.Where(
                room => room.Capacity >= minCapacity);
        }

        if (dto.MaxCapacity.HasValue)
        {
            var maxCapacity = dto.MaxCapacity.Value;

            query = query.Where(
                room => room.Capacity <= maxCapacity);
        }

        // -----------------------------------------------------------
        // Area
        // -----------------------------------------------------------

        if (dto.AreaSquareMetersMin.HasValue)
        {
            var areaMin = dto.AreaSquareMetersMin.Value;

            query = query.Where(
                room => room.AreaSquareMeters.HasValue &&
                        room.AreaSquareMeters.Value >= areaMin);
        }

        if (dto.AreaSquareMetersMax.HasValue)
        {
            var areaMax = dto.AreaSquareMetersMax.Value;

            query = query.Where(
                room => room.AreaSquareMeters.HasValue &&
                        room.AreaSquareMeters.Value <= areaMax);
        }

        // -----------------------------------------------------------
        // Boolean filters
        // -----------------------------------------------------------

        if (dto.IsAccessible.HasValue)
        {
            var isAccessible = dto.IsAccessible.Value;

            query = query.Where(
                room => room.IsAccessible == isAccessible);
        }

        if (dto.IsReservable.HasValue)
        {
            var isReservable = dto.IsReservable.Value;

            query = query.Where(
                room => room.IsReservable == isReservable);
        }

        if (dto.IsActive.HasValue)
        {
            var isActive = dto.IsActive.Value;

            query = query.Where(
                room => room.IsActive == isActive);
        }

        // -----------------------------------------------------------
        // Pagination and ordering
        // -----------------------------------------------------------

        var pageNumber = Math.Max(dto.Page ?? 1, 1);
        var pageSize = Math.Clamp(dto.PageSize ?? 20, 1, 100);

        var totalCount = await query.LongCountAsync(
            cancellationToken);

        var orderedQuery = dto.ColumnOrder == ColumnOrderEnum.DESC
            ? query.OrderByDescending(room => room.Code)
                .ThenBy(room => room.Id)
            : query.OrderBy(room => room.Code)
                .ThenBy(room => room.Id);

        var items = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Page<RoomModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }

    public async Task<IReadOnlyList<RoomModel>> GetByRoomTypeIdAsync(
        string roomTypeId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<RoomModel>()
            .AsNoTracking()
            .Where(room => room.RoomTypeId == roomTypeId)
            .OrderBy(room => room.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoomModel>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.Set<RoomModel>()
            .AsNoTracking()
            .Where(room => room.IsActive)
            .OrderBy(room => room.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoomModel>>
        GetActiveByRoomTypeIdAsync(
            string roomTypeId,
            CancellationToken cancellationToken = default)
    {
        return await context.Set<RoomModel>()
            .AsNoTracking()
            .Where(room =>
                room.RoomTypeId == roomTypeId &&
                room.IsActive)
            .OrderBy(room => room.Name)
            .ToListAsync(cancellationToken);
    }
}