using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;

public sealed record FindReservationAttendeeByIdCommand(
    [property: Required(ErrorMessage = "Reservation ID is required.")]
    string ReservationId,

    [property: Required(ErrorMessage = "Attendee ID is required.")]
    string AttendeeId,

    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId
);