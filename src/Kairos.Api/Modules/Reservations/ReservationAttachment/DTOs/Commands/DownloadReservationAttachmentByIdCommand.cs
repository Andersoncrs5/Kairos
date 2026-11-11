using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;

public sealed record DownloadReservationAttachmentByIdCommand(
    [property: Required(ErrorMessage = "Reservation attachment ID is required.")]
    string AttachmentId,

    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId
);