using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;

public sealed class CancelReservationDto
{
    [StringLength(
        255,
        ErrorMessage = "Cancellation reason cannot exceed 255 characters.")]
    public string? CancellationReason { get; init; }
}