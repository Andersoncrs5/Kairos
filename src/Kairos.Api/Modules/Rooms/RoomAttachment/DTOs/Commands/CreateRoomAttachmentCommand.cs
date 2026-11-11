using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;

public sealed record CreateRoomAttachmentCommand(
    [property: Required(
        ErrorMessage = "Room ID is required.")]
    string RoomId,

    [property: Required(
        ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(
        ErrorMessage = "Room attachment data is required.")]
    CreateRoomAttachmentDto Dto
);