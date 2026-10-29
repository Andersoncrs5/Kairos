using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;

public interface IFindRoomTypeUseCase: IUseCase<string, Result<RoomTypeModel>>
{
    
}