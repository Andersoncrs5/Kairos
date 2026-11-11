using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;

public sealed record DownloadRoomAttachmentByStorageKeyCommand(
    [property: Required(ErrorMessage = "Storage key is required.")]
    string StorageKey,

    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId
);