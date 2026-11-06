using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Equipment.Services.Providers;

public sealed class DeleteEquipmentByIdUseCase(IUnitOfWork uow)
    : IDeleteEquipmentByIdUseCase
{
    public async Task<Result<bool>> ExecuteAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<bool>.ValidationFailure(
                ["Equipment ID is required."]);
        }

        if (!Guid.TryParse(id, out _))
        {
            return Result<bool>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        var equipment = await uow.EquipmentRepository.GetByIdAsync(id, ct);

        if (equipment is null)
        {
            return Result<bool>.NotFound("Equipment doesn't exist.");
        }

        uow.EquipmentRepository.Delete(equipment);

        return Result<bool>.Success(true);
    }
}