using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class RoomOperatingHourMapper
{
    public static partial RoomOperatingHourModel ToModel(
        CreateRoomOperatingHourDto source,
        string Id,
        string roomId,
        DateTimeOffset createdAt);

    public static partial RoomOperatingHourDto ToDto(
        RoomOperatingHourModel source);

    public static partial void ApplyUpdate(
        UpdateRoomOperatingHourDto source,
        [MappingTarget] RoomOperatingHourModel target);
}