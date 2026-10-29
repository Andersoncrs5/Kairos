using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Rooms.Room.DTOs.Request;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Rooms.Room.Services.Interfaces;

public interface IFindAllRoomUseCase
    : IUseCase<FilterRoomDto, Page<RoomModel>>
{
}