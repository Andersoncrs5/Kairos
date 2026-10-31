using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;

public sealed class CreateReservationDto
{
    [Required(ErrorMessage = "Room ID is required.")]
    public required string RoomId { get; init; }

    [Required(ErrorMessage = "Category ID is required.")]
    public required string CategoryId { get; init; }

    [Required(ErrorMessage = "Reservation title is required.")]
    [StringLength(
        150,
        ErrorMessage = "Reservation title cannot exceed 150 characters.")]
    public required string Title { get; init; }

    public string? Description { get; init; }

    [Required(ErrorMessage = "Reservation start date is required.")]
    public required DateTimeOffset StartsAt { get; init; }

    [Required(ErrorMessage = "Reservation end date is required.")]
    public required DateTimeOffset EndsAt { get; init; }

    [StringLength(
        100,
        ErrorMessage = "Idempotency key cannot exceed 100 characters.")]
    public string? IdempotencyKey { get; init; }
}