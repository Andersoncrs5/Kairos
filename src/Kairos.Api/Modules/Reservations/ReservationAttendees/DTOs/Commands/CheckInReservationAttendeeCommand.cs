using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;

public sealed record CheckInReservationAttendeeCommand(
    [property: Required(ErrorMessage = "Reservation ID is required.")]
    string ReservationId,

    [property: Required(ErrorMessage = "Attendee user ID is required.")]
    string AttendeeUserId,

    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId
);