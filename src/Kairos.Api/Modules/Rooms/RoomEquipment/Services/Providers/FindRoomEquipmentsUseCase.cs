using Kairos.Api.Common.Page;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Providers;

public sealed class FindRoomEquipmentsUseCase(IUnitOfWork uow) : IFindRoomEquipmentsUseCase
{
    public async Task<Page<RoomEquipmentModel>> ExecuteAsync(FilterRoomEquipmentDto dto, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(dto);

        return await uow.RoomEquipmentRepository.Find(dto, ct);
    }
}