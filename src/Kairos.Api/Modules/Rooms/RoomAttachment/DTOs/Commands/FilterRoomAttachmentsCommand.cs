using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;

public sealed record FilterRoomAttachmentsCommand(
    [property: Required(
        ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(
        ErrorMessage = "Room attachment filter is required.")]
    FilterRoomAttachmentDto Dto
);