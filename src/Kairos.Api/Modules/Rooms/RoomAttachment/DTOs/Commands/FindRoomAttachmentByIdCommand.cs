using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;

public sealed record FindRoomAttachmentByIdCommand(
    [property: Required(
        ErrorMessage = "Room attachment ID is required.")]
    string AttachmentId,

    [property: Required(
        ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId
);