using Kairos.Api.Common.Page;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Equipment.Services.Providers;

public sealed class FindAllEquipmentUseCase(IUnitOfWork uow): IFindAllEquipmentUseCase
{
    public async Task<Page<EquipmentModel>> ExecuteAsync(FilterEquipmentDto dto, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(dto);

        return await uow.EquipmentRepository.Find(dto, ct);
    }
}