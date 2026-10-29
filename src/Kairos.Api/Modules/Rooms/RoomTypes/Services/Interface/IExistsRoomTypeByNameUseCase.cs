using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;

public interface IExistsRoomTypeByNameUseCase
    : IUseCase<string, Result<bool>>
{
}