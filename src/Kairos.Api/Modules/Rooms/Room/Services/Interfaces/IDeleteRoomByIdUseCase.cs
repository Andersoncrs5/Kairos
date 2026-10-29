using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Rooms.Room.Services.Interfaces;

public interface IDeleteRoomByIdUseCase: IUseCase<string, Result<bool>>
{
}