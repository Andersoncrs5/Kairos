using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;

public sealed class AddReservationAttendeeDto
{
    [Required(ErrorMessage = "Attendee user ID is required.")]
    public required string UserId { get; init; }

    [EnumDataType(
        typeof(AttendeeRoleEnum),
        ErrorMessage = "Attendee role is invalid.")]
    public AttendeeRoleEnum Role { get; init; } = AttendeeRoleEnum.Required;
}