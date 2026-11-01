using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Interfaces;

public interface IUpdateRoomOperatingHourUseCase
    : IUseCase<UpdateRoomOperatingHourCommand, Result<RoomOperatingHourModel>>
{
}