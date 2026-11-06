using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;

public sealed class FindRoomEquipmentByIdUseCase(IUnitOfWork uow) : IFindRoomEquipmentByIdUseCase
{
    public async Task<Result<RoomEquipmentModel>> ExecuteAsync(
        string id,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<RoomEquipmentModel>.ValidationFailure(
                ["Room equipment ID is required."]);
        }

        if (!Guid.TryParse(id, out _))
        {
            return Result<RoomEquipmentModel>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        RoomEquipmentModel? model = await uow.RoomEquipmentRepository.GetByIdAsync(id, ct);
        
        if (model == null)
            return Result<RoomEquipmentModel>.NotFound("Room Equipment not found.");
        
        return Result<RoomEquipmentModel>.Success(model);
    }
}