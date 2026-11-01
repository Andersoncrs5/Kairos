using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;

public interface IRoomOperatingHourRepository
    : IBaseRepository<RoomOperatingHourModel>
{
    Task<Page<RoomOperatingHourModel>> Find(
        FilterRoomOperatingHourDto dto,
        CancellationToken ct = default);

    Task<IReadOnlyList<RoomOperatingHourModel>> GetByRoomIdAsync(
        string roomId,
        CancellationToken ct = default);

    Task<IReadOnlyList<RoomOperatingHourModel>> GetByRoomIdAndDayAsync(
        string roomId,
        DayOfWeek dayOfWeek,
        CancellationToken ct = default);
}