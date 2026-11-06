using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;

public interface IDeleteRoomEquipmentUseCase
    : IUseCase<string, Result<bool>>
{
}