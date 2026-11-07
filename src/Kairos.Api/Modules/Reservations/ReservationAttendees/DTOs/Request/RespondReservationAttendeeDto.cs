using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;

public sealed class RespondReservationAttendeeDto : IValidatableObject
{
    [Required(ErrorMessage = "Attendee status is required.")]
    [EnumDataType(
        typeof(AttendeeStatusEnum),
        ErrorMessage = "Attendee status is invalid.")]
    public required AttendeeStatusEnum Status { get; init; }

    [StringLength(
        255,
        ErrorMessage = "Response note cannot exceed 255 characters.")]
    public string? ResponseNote { get; init; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (Status == AttendeeStatusEnum.Pending)
        {
            yield return new ValidationResult(
                "Attendee status must be Accepted, Declined or Tentative.",
                [nameof(Status)]);
        }
    }
}