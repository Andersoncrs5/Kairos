using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;

public sealed record RespondReservationAttendeeCommand(
    [property: Required(ErrorMessage = "Reservation ID is required.")]
    string ReservationId,

    [property: Required(ErrorMessage = "Attendee user ID is required.")]
    string UserId,

    [property: Required(ErrorMessage = "Response data is required.")]
    RespondReservationAttendeeDto Dto
);