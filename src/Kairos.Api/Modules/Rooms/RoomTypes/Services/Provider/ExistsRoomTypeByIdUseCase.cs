using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Services.Provider;

public sealed class ExistsRoomTypeByIdUseCase(IUnitOfWork uow)
    : IExistsRoomTypeByIdUseCase
{
    public async Task<Result<bool>> ExecuteAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
            return Result<bool>.ValidationFailure(["Room type ID is required."]);

        if (!Guid.TryParse(id, out Guid roomTypeId))
            return Result<bool>.ValidationFailure(["Id must be a valid UUID."]);

        bool exists = await uow.RoomTypeRepository.ExistsAsync(roomTypeId.ToString(), ct);

        return Result<bool>.Success(exists);
    }
}