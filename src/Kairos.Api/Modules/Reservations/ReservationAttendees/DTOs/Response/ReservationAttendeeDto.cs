using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Response;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Response;

public sealed class ReservationAttendeeDto : BaseDTO
{
    public required string ReservationId { get; init; }
    public ReservationDto? Reservation { get; init; }

    public required string UserId { get; init; }
    public UserDTO? User { get; init; }

    public required AttendeeRoleEnum Role { get; init; }

    public required AttendeeStatusEnum Status { get; init; }

    public string? InvitedBy { get; init; }
    public UserDTO? Inviter { get; init; }

    public DateTimeOffset? RespondedAt { get; init; }

    public string? ResponseNote { get; init; }

    public DateTimeOffset? CheckedInAt { get; init; }
}