using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

public interface IFindRoomEquipmentsUseCase: IUseCase<FilterRoomEquipmentDto, Page<RoomEquipmentModel>>
{
    
}