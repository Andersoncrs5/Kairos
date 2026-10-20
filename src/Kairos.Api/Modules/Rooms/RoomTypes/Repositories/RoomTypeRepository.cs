using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Repositories;

public sealed class RoomTypeRepository(AppDbContext context)
    : BaseRepositoryImpl<RoomTypeModel>(context), IRoomTypeRepository
{
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