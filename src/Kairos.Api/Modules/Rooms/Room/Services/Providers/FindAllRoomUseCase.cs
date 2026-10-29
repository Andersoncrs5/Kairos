using Kairos.Api.Common.Page;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.DTOs.Request;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.Room.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Room.Services.Providers;

public sealed class FindAllRoomUseCase(IUnitOfWork uow)
    : IFindAllRoomUseCase
{
    public async Task<Page<RoomModel>> ExecuteAsync(
        FilterRoomDto dto,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(dto);

        return await uow.RoomRepository.Find(dto, ct);
    }
}