using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Models;

public class RoomFeedbackModel : BaseModel
{
    public required string ReservationId { get; set; }
    public ReservationModel? Reservation { get; set; }

    public required string RoomId { get; set; }
    public RoomModel? Room { get; set; }

    public required string UserId { get; set; }
    public UserModel? User { get; set; }

    // 1 a 5
    public required int Rating { get; set; }

    public string? Comment { get; set; }

    public RoomFeedbackIssueTypeEnum? IssueType { get; set; }
}