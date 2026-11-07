using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;

public sealed record AddReservationAttendeeCommand(
    [property: Required(ErrorMessage = "Reservation ID is required.")]
    string ReservationId,

    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(ErrorMessage = "Attendee data is required.")]
    AddReservationAttendeeDto Dto
);