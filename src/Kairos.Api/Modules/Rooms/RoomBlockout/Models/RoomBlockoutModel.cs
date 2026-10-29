using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Models;

public class RoomBlockoutModel : BaseModel
{
    public required string RoomId { get; set; }
    public RoomModel? Room { get; set; }

    public required RoomBlockoutType Type { get; set; }

    public RoomBlockoutStatus Status { get; set; } = RoomBlockoutStatus.Scheduled;

    public required string Reason { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public string? CancelledBy { get; set; }
    public UserModel? Canceller { get; set; }

    public required string CreatedBy { get; set; }
    public UserModel? Creater { get; set; }
}