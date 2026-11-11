using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;

public sealed record DownloadReservationAttachmentByStorageKeyCommand(
    [property: Required(ErrorMessage = "Storage key is required.")]
    string StorageKey,

    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId
);