using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;

public interface IFindAllRoomTypeUseCase: IUseCase<FilterRoomTypeDto, Page<RoomTypeModel>>
{
}