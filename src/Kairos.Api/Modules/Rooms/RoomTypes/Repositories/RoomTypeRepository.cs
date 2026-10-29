using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Repositories;

public sealed class RoomTypeRepository(AppDbContext context)
    : BaseRepositoryImpl<RoomTypeModel>(context), IRoomTypeRepository
{
    public async Task<Page<RoomTypeModel>> Find(
        FilterRoomTypeDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var query = context.Set<RoomTypeModel>()
            .AsNoTracking();

        // Identificador
        if (!string.IsNullOrWhiteSpace(dto.Id))
        {
            query = query.Where(roomType =>
                roomType.Id == dto.Id);
        }

        // Versão
        if (dto.VersionMin.HasValue)
        {
            query = query.Where(roomType =>
                roomType.Version >= dto.VersionMin.Value);
        }

        if (dto.VersionMax.HasValue)
        {
            query = query.Where(roomType =>
                roomType.Version <= dto.VersionMax.Value);
        }

        // Data de criação
        if (dto.CreatedAtMin.HasValue)
        {
            query = query.Where(roomType =>
                roomType.CreatedAt >= dto.CreatedAtMin.Value);
        }

        if (dto.CreatedAtMax.HasValue)
        {
            query = query.Where(roomType =>
                roomType.CreatedAt <= dto.CreatedAtMax.Value);
        }

        // Data de atualização
        if (dto.UpdatedAtMin.HasValue)
        {
            query = query.Where(roomType =>
                roomType.UpdatedAt.HasValue &&
                roomType.UpdatedAt.Value >= dto.UpdatedAtMin.Value);
        }

        if (dto.UpdatedAtMax.HasValue)
        {
            query = query.Where(roomType =>
                roomType.UpdatedAt.HasValue &&
                roomType.UpdatedAt.Value <= dto.UpdatedAtMax.Value);
        }

        // Nome
        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            string name = dto.Name.Trim();
            string pattern = $"%{name}%";

            query = dto.NameIgnoreCase == true
                ? query.Where(roomType =>
                    EF.Functions.ILike(roomType.Name, pattern))
                : query.Where(roomType =>
                    EF.Functions.Like(roomType.Name, pattern));
        }

        // Descrição
        if (!string.IsNullOrWhiteSpace(dto.Description))
        {
            string description = dto.Description.Trim();
            string pattern = $"%{description}%";

            query = dto.DescriptionIgnoreCase == true
                ? query.Where(roomType =>
                    roomType.Description != null &&
                    EF.Functions.ILike(
                        roomType.Description,
                        pattern))
                : query.Where(roomType =>
                    roomType.Description != null &&
                    EF.Functions.Like(
                        roomType.Description,
                        pattern));
        }

        // Estado
        if (dto.IsActive.HasValue)
        {
            query = query.Where(roomType =>
                roomType.IsActive == dto.IsActive.Value);
        }

        // Total após os filtros, antes da paginação
        long totalCount = await query.LongCountAsync(ct);

        // Paginação
        int pageNumber = Math.Max(dto.Page ?? 1, 1);
        int pageSize = Math.Clamp(dto.PageSize ?? 20, 1, 100);

        // Ordenação determinística pelo nome e pelo ID
        query = dto.ColumnOrder == ColumnOrderEnum.DESC
            ? query
                .OrderByDescending(roomType => roomType.Name)
                .ThenByDescending(roomType => roomType.Id)
            : query
                .OrderBy(roomType => roomType.Name)
                .ThenBy(roomType => roomType.Id);

        long offset = ((long)pageNumber - 1) * pageSize;
        int skip = (int)Math.Min(offset, int.MaxValue);

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return Page<RoomTypeModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
    
    public Task<RoomTypeModel?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return context.Set<RoomTypeModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                roomType => roomType.Name == name,
                cancellationToken);
    }

    public Task<bool> ExistsByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return context.Set<RoomTypeModel>()
            .AnyAsync(
                roomType => roomType.Name == name,
                cancellationToken);
    }

    public async Task<IReadOnlyList<RoomTypeModel>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.Set<RoomTypeModel>()
            .AsNoTracking()
            .Where(roomType => roomType.IsActive)
            .OrderBy(roomType => roomType.Name)
            .ToListAsync(cancellationToken);
    }
}