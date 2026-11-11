using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;

public sealed record UpdateReservationAttachmentCommand(
    [property: Required(ErrorMessage = "Reservation attachment ID is required.")]
    string AttachmentId,

    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(ErrorMessage = "Reservation attachment data is required.")]
    UpdateReservationAttachmentDto Dto
);