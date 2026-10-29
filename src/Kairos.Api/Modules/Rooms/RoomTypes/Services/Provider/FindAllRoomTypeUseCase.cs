using Kairos.Api.Common.Page;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Services.Provider;

public sealed class FindAllRoomTypeUseCase(IUnitOfWork uow)
    : IFindAllRoomTypeUseCase
{
    public async Task<Page<RoomTypeModel>> ExecuteAsync(
        FilterRoomTypeDto dto,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(dto);

        return await uow.RoomTypeRepository.Find(dto, ct);
    }
}