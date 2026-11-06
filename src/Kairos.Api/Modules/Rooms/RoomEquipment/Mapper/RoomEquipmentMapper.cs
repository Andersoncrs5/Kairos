using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class RoomEquipmentMapper
{
    public static partial RoomEquipmentModel ToModel(AddEquipmentToRoomDto source, string id, DateTimeOffset createdAt);

    public static partial RoomEquipmentDto ToDto(RoomEquipmentModel source);

    public static partial void ApplyUpdate(
        UpdateRoomEquipmentDto source,
        [MappingTarget] RoomEquipmentModel target);
}