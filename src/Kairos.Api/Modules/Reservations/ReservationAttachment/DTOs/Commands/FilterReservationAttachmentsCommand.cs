using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;

public sealed record FilterReservationAttachmentsCommand(
    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(ErrorMessage = "Reservation attachment filter is required.")]
    FilterReservationAttachmentDto Dto
);