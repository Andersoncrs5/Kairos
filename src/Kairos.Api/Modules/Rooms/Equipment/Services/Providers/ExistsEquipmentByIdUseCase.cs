using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Equipment.Services.Providers;

public sealed class ExistsEquipmentByIdUseCase(IUnitOfWork uow)
    : IExistsEquipmentByIdUseCase
{
    public async Task<Result<bool>> ExecuteAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
            return Result<bool>.ValidationFailure(["Equipment ID is required."]);

        if (!Guid.TryParse(id, out Guid equipmentId))
            return Result<bool>.ValidationFailure(["Id must be a valid UUID."]);

        bool exists = await uow.EquipmentRepository.ExistsAsync(equipmentId.ToString(), ct);

        return Result<bool>.Success(exists);
    }
}