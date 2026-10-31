using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;

namespace Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;

public sealed record UpdateReservationCommand(
    [property: Required(
        ErrorMessage = "Reservation ID is required.")]
    string ReservationId,

    [property: Required(
        ErrorMessage = "Reservation update data is required.")]
    UpdateReservationDto Dto
);