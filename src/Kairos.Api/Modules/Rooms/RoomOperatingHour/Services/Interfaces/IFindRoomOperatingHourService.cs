using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Interfaces;

public interface IFindRoomOperatingHourService
    : IUseCase<
        FilterRoomOperatingHourDto,
        Result<Page<RoomOperatingHourModel>>>
{
}