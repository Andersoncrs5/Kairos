using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Config.DB;
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
}