using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;

public sealed class CreateRoomBlockoutDto : IValidatableObject
{
    [Required(ErrorMessage = "Room ID is required.")]
    [StringLength(450, MinimumLength = 1, ErrorMessage = "Room ID must be between 1 and 450 characters.")]
    public required string RoomId { get; init; }

    [Required(ErrorMessage = "Room blockout type is required.")]
    [EnumDataType(
        typeof(RoomBlockoutType),
        ErrorMessage = "Room blockout type is invalid.")]
    public required RoomBlockoutType Type { get; init; }

    [Required(ErrorMessage = "Room blockout reason is required.")]
    [StringLength(
        255,
        MinimumLength = 1,
        ErrorMessage = "Room blockout reason must be between 1 and 255 characters.")]
    public required string Reason { get; init; }

    [StringLength(
        1000,
        ErrorMessage = "Room blockout notes cannot exceed 1000 characters.")]
    public string? Notes { get; init; }

    [Required(ErrorMessage = "Room blockout start date is required.")]
    public required DateTimeOffset StartsAt { get; init; }

    [Required(ErrorMessage = "Room blockout end date is required.")]
    public required DateTimeOffset EndsAt { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt != default && EndsAt != default && StartsAt >= EndsAt)
        {
            yield return new ValidationResult(
                "Room blockout start date must be earlier than end date.",
                [nameof(StartsAt), nameof(EndsAt)]);
        }
    }
}