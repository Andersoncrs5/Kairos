using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Services.Provider;

public sealed class FindRoomTypeUseCase(IUnitOfWork uow): IFindRoomTypeUseCase
{
    public async Task<Result<RoomTypeModel>> ExecuteAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
            return Result<RoomTypeModel>.ValidationFailure(["Room type ID is required."]);

        if (!Guid.TryParse(id, out _))
            return Result<RoomTypeModel>.ValidationFailure(["Id must be a valid UUID."]);

        RoomTypeModel? roomType =
            await uow.RoomTypeRepository.GetByIdAsync(
                id,
                ct);

        if (roomType is null)
            return Result<RoomTypeModel>.NotFound("Room type doesn't exist.");

        return Result<RoomTypeModel>.Success(roomType);
    }
}