using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;

namespace Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;

public sealed record CancelReservationCommand(
    [property: Required(ErrorMessage = "Reservation ID is required.")]
    string ReservationId,

    [property: Required(ErrorMessage = "Cancellation user ID is required.")]
    string UserId,

    [property: Required(ErrorMessage = "Cancellation data is required.")]
    CancelReservationDto Dto
);