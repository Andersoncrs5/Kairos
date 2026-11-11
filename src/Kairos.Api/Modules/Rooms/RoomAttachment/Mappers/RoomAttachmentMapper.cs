using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Mappers;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class RoomAttachmentMapper
{
    [MapperIgnoreSource(nameof(CreateRoomAttachmentDto.File))]
    public static partial RoomAttachmentModel ToModel(
        CreateRoomAttachmentDto source,
        string id,
        string roomId,
        string storageKey,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        string? createdBy,
        DateTimeOffset createdAt);

    [MapperIgnoreSource(nameof(RoomAttachmentModel.StorageKey))]
    [MapperIgnoreSource(nameof(RoomAttachmentModel.Room))]
    [MapperIgnoreSource(nameof(RoomAttachmentModel.Creator))]
    public static partial RoomAttachmentDto ToDto(
        RoomAttachmentModel source,
        string? downloadUrl = null,
        RoomDto? room = null,
        UserDTO? creator = null);

    public static partial void ApplyUpdate(
        UpdateRoomAttachmentDto source,
        [MappingTarget] RoomAttachmentModel target);
}