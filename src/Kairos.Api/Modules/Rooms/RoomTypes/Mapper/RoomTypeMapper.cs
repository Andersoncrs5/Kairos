using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class RoomTypeMapper
{
    public static partial RoomTypeModel ToModel(
        CreateRoomTypeDto source,
        string Id,
        DateTimeOffset createdAt);

    public static partial RoomTypeDto ToDto(
        RoomTypeModel source);

    public static partial void ApplyUpdate(
        UpdateRoomTypeDto source,
        [MappingTarget] RoomTypeModel target);
}