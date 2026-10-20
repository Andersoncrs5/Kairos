using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.Room.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Rooms.Room.Repositories;

public sealed class RoomRepository(AppDbContext context)
    : BaseRepositoryImpl<RoomModel>(context), IRoomRepository
{
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

    public async Task<IReadOnlyList<RoomModel>> GetActiveByRoomTypeIdAsync(
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