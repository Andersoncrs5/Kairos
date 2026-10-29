using Kairos.Api.Modules.Rooms.Room.DTOs.Request;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Rooms.Room.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class RoomMapper
{
    public static partial RoomModel ToModel(CreateRoomDto source, string Id, DateTimeOffset createdAt);

    public static partial RoomDto ToDto(RoomModel source);

    public static partial void ApplyUpdate(UpdateRoomDto source, [MappingTarget] RoomModel target);
}