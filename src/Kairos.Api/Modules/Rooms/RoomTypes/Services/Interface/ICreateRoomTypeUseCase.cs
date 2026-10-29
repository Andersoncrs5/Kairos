using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;

public interface ICreateRoomTypeUseCase
    : IUseCase<CreateRoomTypeDto, Result<RoomTypeModel>>
{
}