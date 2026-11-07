using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.Reservation.Models;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Models;

public class ReservationAttendeeModel : BaseModel
{
    public required string ReservationId { get; set; }
    public ReservationModel? Reservation { get; set; }

    public required string UserId { get; set; }
    public UserModel? User { get; set; }

    public AttendeeRoleEnum Role { get; set; } = AttendeeRoleEnum.Required;

    public AttendeeStatusEnum Status { get; set; } = AttendeeStatusEnum.Pending;

    public string? InvitedBy { get; set; }
    public UserModel? Inviter { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }
    public string? ResponseNote { get; set; }

    public DateTimeOffset? CheckedInAt { get; set; }
}