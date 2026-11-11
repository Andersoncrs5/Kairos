using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;

public sealed record CreateReservationAttachmentCommand(
    [property: Required(ErrorMessage = "Reservation ID is required.")]
    string ReservationId,

    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(ErrorMessage = "Reservation attachment data is required.")]
    CreateReservationAttachmentDto Dto
);