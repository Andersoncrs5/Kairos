using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;

public sealed class FilterReservationAttendeeDto : BaseFilter
{
    public string? ReservationId { get; set; }
    [StringLength(150, ErrorMessage = "Reservation title cannot exceed 150 characters.")]
    public string? ReservationTitle { get; init; }
    public bool? ReservationTitleIgnoreCase { get; init; }
    [EnumDataType(typeof(ReservationStatusEnum), ErrorMessage = "Reservation status is invalid.")]
    public ReservationStatusEnum? ReservationStatus { get; init; }

    public string? UserId { get; set; }
    public string? UserName { get; init; }
    public bool? UserNameIgnoreCase { get; init; }
    public string? Email { get; init; }
    public bool? EmailIgnoreCase { get; init; }

    [EnumDataType(typeof(AttendeeRoleEnum), ErrorMessage = "Attendee role is invalid.")]
    public AttendeeRoleEnum? Role { get; init; }

    [EnumDataType(typeof(AttendeeStatusEnum), ErrorMessage = "Attendee status is invalid.")]
    public AttendeeStatusEnum? Status { get; init; }

    public string? InvitedBy { get; init; }

    public DateTimeOffset? RespondedAtFrom { get; init; }
    public DateTimeOffset? RespondedAtTo { get; init; }

    public DateTimeOffset? CheckedInAtFrom { get; init; }
    public DateTimeOffset? CheckedInAtTo { get; init; }

    public DateTimeOffset? ReservationStartsAtFrom { get; init; }
    public DateTimeOffset? ReservationStartsAtTo { get; init; }
}