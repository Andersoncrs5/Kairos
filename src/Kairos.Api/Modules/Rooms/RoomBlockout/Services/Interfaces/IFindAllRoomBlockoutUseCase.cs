using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;

public interface IFindAllRoomBlockoutUseCase
    : IUseCase<
        FilterRoomBlockoutDto,
        Result<Page<RoomBlockoutModel>>>
{
}