using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Equipment.Services.Providers;

public sealed class FindEquipmentByIdUseCase(IUnitOfWork uow) : IFindEquipmentByIdUseCase
{
    public async Task<Result<EquipmentModel>> ExecuteAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
            return Result<EquipmentModel>.ValidationFailure(["Equipment ID is required."]);

        if (!Guid.TryParse(id, out _))
            return Result<EquipmentModel>.ValidationFailure(["Id must be a valid UUID."]);

        EquipmentModel? equipment =
            await uow.EquipmentRepository.GetByIdAsync(
                id,
                ct);

        if (equipment is null)
            return Result<EquipmentModel>.NotFound("Equipment doesn't exist.");

        return Result<EquipmentModel>.Success(equipment);
    }
}