using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Rooms.Equipment.Repositories;

public sealed class EquipmentRepository(AppDbContext context)
    : BaseRepositoryImpl<EquipmentModel>(context), IEquipmentRepository
{
    public async Task<Page<EquipmentModel>> Find(FilterEquipmentDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var query = context.Set<EquipmentModel>()
            .AsNoTracking();

        // Identificador
        if (!string.IsNullOrWhiteSpace(dto.Id))
        {
            query = query.Where(equipment =>
                equipment.Id == dto.Id);
        }

        // Versão
        if (dto.VersionMin.HasValue)
        {
            query = query.Where(equipment =>
                equipment.Version >= dto.VersionMin.Value);
        }

        if (dto.VersionMax.HasValue)
        {
            query = query.Where(equipment =>
                equipment.Version <= dto.VersionMax.Value);
        }

        // Data de criação
        if (dto.CreatedAtMin.HasValue)
        {
            query = query.Where(equipment =>
                equipment.CreatedAt >= dto.CreatedAtMin.Value);
        }

        if (dto.CreatedAtMax.HasValue)
        {
            query = query.Where(equipment =>
                equipment.CreatedAt <= dto.CreatedAtMax.Value);
        }

        // Data de atualização
        if (dto.UpdatedAtMin.HasValue)
        {
            query = query.Where(equipment =>
                equipment.UpdatedAt.HasValue &&
                equipment.UpdatedAt.Value >= dto.UpdatedAtMin.Value);
        }

        if (dto.UpdatedAtMax.HasValue)
        {
            query = query.Where(equipment =>
                equipment.UpdatedAt.HasValue &&
                equipment.UpdatedAt.Value <= dto.UpdatedAtMax.Value);
        }

        // Código
        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            string code = dto.Code.Trim();
            string pattern = $"%{code}%";

            query = query.Where(equipment =>
                EF.Functions.Like(equipment.Code, pattern));
        }

        // Nome
        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            string name = dto.Name.Trim();
            string pattern = $"%{name}%";

            query = dto.NameIgnoreCase == true
                ? query.Where(equipment =>
                    EF.Functions.ILike(equipment.Name, pattern))
                : query.Where(equipment =>
                    EF.Functions.Like(equipment.Name, pattern));
        }

        // Descrição
        if (!string.IsNullOrWhiteSpace(dto.Description))
        {
            string description = dto.Description.Trim();
            string pattern = $"%{description}%";

            query = query.Where(equipment =>
                equipment.Description != null &&
                EF.Functions.Like(equipment.Description, pattern));
        }

        // Estado
        if (dto.IsActive.HasValue)
        {
            query = query.Where(equipment =>
                equipment.IsActive == dto.IsActive.Value);
        }

        // Total após os filtros, antes da paginação
        long totalCount = await query.LongCountAsync(ct);

        // Paginação
        int pageNumber = Math.Max(dto.Page ?? 1, 1);
        int pageSize = Math.Clamp(dto.PageSize ?? 20, 1, 100);

        // Ordenação determinística pelo nome e pelo ID
        query = dto.ColumnOrder == ColumnOrderEnum.DESC
            ? query
                .OrderByDescending(equipment => equipment.Name)
                .ThenByDescending(equipment => equipment.Id)
            : query
                .OrderBy(equipment => equipment.Name)
                .ThenBy(equipment => equipment.Id);

        long offset = ((long)pageNumber - 1) * pageSize;
        int skip = (int)Math.Min(offset, int.MaxValue);

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return Page<EquipmentModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }

    public Task<EquipmentModel?> GetByCodeAsync(
        string code,
        CancellationToken ct = default)
    {
        return context.Set<EquipmentModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                equipment => equipment.Code == code,
                ct);
    }

    public Task<bool> ExistsByCodeAsync(
        string code,
        CancellationToken ct = default)
    {
        return context.Set<EquipmentModel>()
            .AnyAsync(
                equipment => equipment.Code == code,
                ct);
    }

    public Task<EquipmentModel?> GetByNameAsync(
        string name,
        CancellationToken ct = default)
    {
        return context.Set<EquipmentModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(equipment => equipment.Name == name, ct);
    }

    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
    {
        return context.Set<EquipmentModel>()
            .AnyAsync(
                equipment => equipment.Name == name,
                ct);
    }

    public async Task<IReadOnlyList<EquipmentModel>> GetActiveAsync(
        CancellationToken ct = default)
    {
        return await context.Set<EquipmentModel>()
            .AsNoTracking()
            .Where(equipment => equipment.IsActive)
            .OrderBy(equipment => equipment.Name)
            .ToListAsync(ct);
    }
}