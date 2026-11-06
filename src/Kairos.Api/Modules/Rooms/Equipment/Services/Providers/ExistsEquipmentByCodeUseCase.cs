using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Equipment.Services.Providers;

public sealed class ExistsEquipmentByCodeUseCase(IUnitOfWork uow)
    : IExistsEquipmentByCodeUseCase
{
    public async Task<Result<bool>> ExecuteAsync(string code, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(code))
            return Result<bool>.ValidationFailure(["Equipment code is required."]);

        string trimmedCode = code.Trim();

        bool exists = await uow.EquipmentRepository.ExistsByCodeAsync(trimmedCode, ct);

        return Result<bool>.Success(exists);
    }
}