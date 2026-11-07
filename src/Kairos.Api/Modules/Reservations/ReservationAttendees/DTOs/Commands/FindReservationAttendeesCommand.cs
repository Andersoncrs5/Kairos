using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;

public sealed record FindReservationAttendeesCommand(
    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(ErrorMessage = "Attendee filter is required.")]
    FilterReservationAttendeeDto Dto
);