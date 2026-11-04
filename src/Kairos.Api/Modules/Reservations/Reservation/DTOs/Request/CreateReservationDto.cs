using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;

public sealed class CreateReservationDto : IValidatableObject
{
    [Required(ErrorMessage = "Room ID is required.")]
    [StringLength(450, ErrorMessage = "Room ID cannot exceed 450 characters.")]
    public required string RoomId { get; init; }

    [Required(ErrorMessage = "Category ID is required.")]
    [StringLength(450, ErrorMessage = "Category ID cannot exceed 450 characters.")]
    public required string CategoryId { get; init; }

    [Required(ErrorMessage = "Reservation title is required.")]
    [StringLength(
        150,
        MinimumLength = 1,
        ErrorMessage = "Reservation title must be between 1 and 150 characters.")]
    public required string Title { get; init; }

    [StringLength(
        1000,
        ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; init; }

    [Required(ErrorMessage = "Reservation start date is required.")]
    public required DateTimeOffset StartsAt { get; init; }

    [Required(ErrorMessage = "Reservation end date is required.")]
    public required DateTimeOffset EndsAt { get; init; }

    [StringLength(
        100,
        ErrorMessage = "Idempotency key cannot exceed 100 characters.")]
    public string? IdempotencyKey { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt != default && EndsAt != default && StartsAt >= EndsAt)
        {
            yield return new ValidationResult(
                "Reservation start date must be earlier than end date.",
                [nameof(StartsAt), nameof(EndsAt)]);
        }
    }
}