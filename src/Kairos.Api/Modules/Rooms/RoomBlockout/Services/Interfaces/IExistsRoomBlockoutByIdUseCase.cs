using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;

public interface IExistsRoomBlockoutByIdUseCase: IUseCase<string, Result<bool>>
{
}