using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;

public sealed class UpdateReservationDto : IValidatableObject
{
    [StringLength(450, ErrorMessage = "Room ID cannot exceed 450 characters.")]
    public string? RoomId { get; init; }

    [StringLength(450, ErrorMessage = "Category ID cannot exceed 450 characters.")]
    public string? CategoryId { get; init; }

    [StringLength(
        150,
        MinimumLength = 1,
        ErrorMessage = "Reservation title must be between 1 and 150 characters.")]
    public string? Title { get; init; }

    [StringLength(
        1000,
        ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt.HasValue && EndsAt.HasValue && StartsAt.Value >= EndsAt.Value)
        {
            yield return new ValidationResult(
                "Reservation start date must be earlier than end date.",
                [nameof(StartsAt), nameof(EndsAt)]);
        }
    }
}