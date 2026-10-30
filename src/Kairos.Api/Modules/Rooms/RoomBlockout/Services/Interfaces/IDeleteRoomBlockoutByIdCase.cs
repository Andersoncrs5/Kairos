using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;

public interface IDeleteRoomBlockoutByIdCase
    : IUseCase<string, Result<bool>>
{
}