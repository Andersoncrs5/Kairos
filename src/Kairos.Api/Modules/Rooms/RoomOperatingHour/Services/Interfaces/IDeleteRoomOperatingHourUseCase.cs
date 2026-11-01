using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Interfaces;

public interface IDeleteRoomOperatingHourUseCase
    : IUseCase<string, Result<bool>>
{
}