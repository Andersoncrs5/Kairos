using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Reservations.Reservation.Models;

public class ReservationModel : BaseModel
{
    public string RoomId { get; set; }
    public RoomModel? Room { get; set; }

    public required string UserId { get; set; }
    public UserModel? User { get; set; }

    public string CategoryId { get; set; }
    public CategoryModel? Category { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }

    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }

    public ReservationStatusEnum Status { get; set; }
        = ReservationStatusEnum.Confirmed;

    public string? IdempotencyKey { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancelledBy { get; set; }
    public UserModel? Canceller { get; set; }

    public string? CancellationReason { get; set; }

    public DateTimeOffset? CheckedInAt { get; set; }
    public DateTimeOffset? CheckedOutAt { get; set; }
}