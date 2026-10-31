using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;

public sealed record CancelReservationCommand(
    [property: Required(
        ErrorMessage = "Reservation ID is required.")]
    string ReservationId,

    [property: StringLength(
        255,
        ErrorMessage = "Reservation cancellation reason cannot exceed 255 characters.")]
    string? CancellationReason,

    [property: Required(
        ErrorMessage = "Reservation canceller ID is required.")]
    string CancelledBy
);