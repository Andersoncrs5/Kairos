using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;

public sealed record UpdateReservationAttendeeCommand(
    [property: Required(ErrorMessage = "Reservation ID is required.")]
    string ReservationId,

    [property: Required(ErrorMessage = "Attendee ID is required.")]
    string AttendeeId,

    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(ErrorMessage = "Attendee update data is required.")]
    UpdateReservationAttendeeDto Dto
);