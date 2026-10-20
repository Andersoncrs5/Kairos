using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Repositories;

public interface IRoomTypeRepository
    : IBaseRepository<RoomTypeModel>
{
    Task<RoomTypeModel?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoomTypeModel>> GetActiveAsync(
        CancellationToken cancellationToken = default);
}