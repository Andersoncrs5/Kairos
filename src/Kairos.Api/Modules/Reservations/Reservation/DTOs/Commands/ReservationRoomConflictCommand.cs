using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;

public sealed record ReservationRoomConflictCommand(
    [property: Required(ErrorMessage = "Room ID is required.")]
    string RoomId,

    DateTimeOffset StartsAt,

    DateTimeOffset EndsAt
);