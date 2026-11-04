using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;

public sealed class CreateRoomOperatingHourDto : IValidatableObject
{
    [EnumDataType(
        typeof(DayOfWeek),
        ErrorMessage = "Invalid day of week.")]
    public DayOfWeek DayOfWeek { get; init; }

    public TimeOnly OpensAt { get; init; }

    public TimeOnly ClosesAt { get; init; }

    public bool IsClosed { get; init; }

    [StringLength(
        500,
        ErrorMessage = "Room operating hour notes cannot exceed 500 characters.")]
    public string? Notes { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsClosed && OpensAt >= ClosesAt)
        {
            yield return new ValidationResult(
                "Opening time must be earlier than closing time.",
                [nameof(OpensAt), nameof(ClosesAt)]);
        }
    }
}