using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;

public sealed class UpdateReservationDto
{
    public string? RoomId { get; init; }

    public string? CategoryId { get; init; }

    [StringLength(
        150,
        ErrorMessage = "Reservation title cannot exceed 150 characters.")]
    public string? Title { get; init; }

    public string? Description { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }
}