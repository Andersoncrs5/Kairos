using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.Room.DTOs.Commands;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Rooms.Room.Services.Interfaces;

public interface IUpdateRoomUseCase: IUseCase<UpdateRoomCommand, Result<RoomModel>>
{
}