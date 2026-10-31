using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;

namespace Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;

public sealed class CreateReservationCommand
{
    [Required(ErrorMessage = "Reservation data is required.")]
    public required CreateReservationDto Dto { get; init; }

    [Required(ErrorMessage = "Reservation user ID is required.")]
    public required string UserId { get; init; }
}