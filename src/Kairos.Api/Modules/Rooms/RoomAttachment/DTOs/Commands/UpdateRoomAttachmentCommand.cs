using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;

public sealed record UpdateRoomAttachmentCommand(
    [property: Required(
        ErrorMessage = "Room attachment ID is required.")]
    string AttachmentId,

    [property: Required(
        ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(
        ErrorMessage = "Room attachment data is required.")]
    UpdateRoomAttachmentDto Dto
);