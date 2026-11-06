using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Repositories;

public sealed class RoomEquipmentRepository(AppDbContext context)
    : BaseRepositoryImpl<RoomEquipmentModel>(context), IRoomEquipmentRepository
{
    public async Task<Page<RoomEquipmentModel>> Find(
        FilterRoomEquipmentDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        IQueryable<RoomEquipmentModel> query = context.Set<RoomEquipmentModel>()
            .Include(x => x.Room)
            .Include(x => x.Equipment)
            .AsNoTracking();

        // Identificador
        if (!string.IsNullOrWhiteSpace(dto.Id))
        {
            query = query.Where(x => x.Id == dto.Id);
        }

        // Room ID
        if (!string.IsNullOrWhiteSpace(dto.RoomId))
        {
            query = query.Where(x => x.RoomId == dto.RoomId);
        }

        // Código da Sala
        if (!string.IsNullOrWhiteSpace(dto.CodeRoom))
        {
            string codeRoom = dto.CodeRoom.Trim();
            string pattern = $"%{codeRoom}%";

            query = dto.CodeRoomIgnoreCase == true
                ? query.Where(x => x.Room != null && EF.Functions.ILike(x.Room.Code, pattern))
                : query.Where(x => x.Room != null && EF.Functions.Like(x.Room.Code, pattern));
        }

        // Nome da Sala
        if (!string.IsNullOrWhiteSpace(dto.NameRoom))
        {
            string nameRoom = dto.NameRoom.Trim();
            string pattern = $"%{nameRoom}%";

            query = dto.NameRoomIgnoreCase == true
                ? query.Where(x => x.Room != null && EF.Functions.ILike(x.Room.Name, pattern))
                : query.Where(x => x.Room != null && EF.Functions.Like(x.Room.Name, pattern));
        }

        // Equipment ID
        if (!string.IsNullOrWhiteSpace(dto.EquipmentId))
        {
            query = query.Where(x => x.EquipmentId == dto.EquipmentId);
        }

        // Nome do Equipamento
        if (!string.IsNullOrWhiteSpace(dto.Name))
        {
            string name = dto.Name.Trim();
            string pattern = $"%{name}%";

            query = dto.NameIgnoreCase == true
                ? query.Where(x => x.Equipment != null && EF.Functions.ILike(x.Equipment.Name, pattern))
                : query.Where(x => x.Equipment != null && EF.Functions.Like(x.Equipment.Name, pattern));
        }

        // Quantidade (Mínima e Máxima)
        if (dto.QuantityMin.HasValue)
        {
            query = query.Where(x => x.Quantity >= dto.QuantityMin.Value);
        }

        if (dto.QuantityMax.HasValue)
        {
            query = query.Where(x => x.Quantity <= dto.QuantityMax.Value);
        }

        // Notas
        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            string notes = dto.Notes.Trim();
            string pattern = $"%{notes}%";

            query = dto.NotesIgnoreCase == true
                ? query.Where(x => x.Notes != null && EF.Functions.ILike(x.Notes, pattern))
                : query.Where(x => x.Notes != null && EF.Functions.Like(x.Notes, pattern));
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
            query = query.Where(x => x.CreatedAt >= dto.CreatedAtMin.Value);
        }

        if (dto.CreatedAtMax.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= dto.CreatedAtMax.Value);
        }

        long totalCount = await query.LongCountAsync(ct);

        // Paginação
        int pageNumber = Math.Max(dto.Page ?? 1, 1);
        int pageSize = Math.Clamp(dto.PageSize ?? 20, 1, 100);

        query = dto.ColumnOrder == ColumnOrderEnum.DESC
            ? query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
            : query
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id);

        long offset = ((long)pageNumber - 1) * pageSize;
        int skip = (int)Math.Min(offset, int.MaxValue);

        List<RoomEquipmentModel> items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return Page<RoomEquipmentModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }

    public Task<RoomEquipmentModel?> GetByRoomAndEquipmentAsync(
        string roomId,
        string equipmentId,
        CancellationToken ct = default)
    {
        return context.Set<RoomEquipmentModel>()
            .Include(x => x.Room)
            .Include(x => x.Equipment)
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.RoomId == roomId && x.EquipmentId == equipmentId,
                ct);
    }

    public Task<bool> ExistsByRoomAndEquipmentAsync(
        string roomId,
        string equipmentId,
        CancellationToken ct = default)
    {
        return context.Set<RoomEquipmentModel>()
            .AnyAsync(
                x => x.RoomId == roomId && x.EquipmentId == equipmentId,
                ct);
    }

    public async Task<IReadOnlyList<RoomEquipmentModel>> GetByRoomIdAsync(
        string roomId,
        CancellationToken ct = default)
    {
        return await context.Set<RoomEquipmentModel>()
            .Include(x => x.Equipment)
            .AsNoTracking()
            .Where(x => x.RoomId == roomId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);
    }
}