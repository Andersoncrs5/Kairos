using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;

public sealed class UpdateReservationAttendeeDto
{
    [Required(ErrorMessage = "Attendee role is required.")]
    [EnumDataType(
        typeof(AttendeeRoleEnum),
        ErrorMessage = "Attendee role is invalid.")]
    public required AttendeeRoleEnum Role { get; init; }
}