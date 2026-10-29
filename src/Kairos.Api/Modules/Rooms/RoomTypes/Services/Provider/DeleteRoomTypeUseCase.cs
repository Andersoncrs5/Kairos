using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Services.Provider;

public sealed class DeleteRoomTypeUseCase(IUnitOfWork uow)
    : IDeleteRoomTypeUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string id,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<bool>.ValidationFailure(
                ["Room type ID is required."]);
        }

        if (!Guid.TryParse(id, out _))
        {
            return Result<bool>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        var roomType = await uow.RoomTypeRepository.GetByIdAsync(
            id,
            ct);

        if (roomType is null)
        {
            return Result<bool>.NotFound(
                "Room type doesn't exist.");
        }

        uow.RoomTypeRepository.Delete(roomType);

        return Result<bool>.Success(true);
    }
}