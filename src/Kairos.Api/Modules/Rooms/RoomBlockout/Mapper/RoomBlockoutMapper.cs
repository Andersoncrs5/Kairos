using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class RoomBlockoutMapper
{
    public static partial RoomBlockoutModel ToModel(
        CreateRoomBlockoutDto source,
        string Id,
        DateTimeOffset createdAt,
        string createdBy);

    public static partial RoomBlockoutDto ToDto(
        RoomBlockoutModel source);

    public static partial void ApplyUpdate(
        UpdateRoomBlockoutDto source,
        [MappingTarget] RoomBlockoutModel target);
}