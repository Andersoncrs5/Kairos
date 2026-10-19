using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Reservations.ReservationAttendee.Models;


public class ReservationAttendeeModel : BaseModel
{
    public required string ReservationId { get; set; }

    public required string UserId { get; set; }

    public AttendeeStatus Status { get; set; } = AttendeeStatus.Pending;
}

public enum AttendeeStatus
{
    Pending,
    Accepted,
    Declined
}