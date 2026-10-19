using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Reservations.Reservation.Models;

public class ReservationModel: BaseModel
{
    public string RoomId { get; set; }

    public required string UserId { get; set; }

    public string CategoryId { get; set; }

    public required string Title { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public ReservationStatusEnum Status { get; set; } =
        ReservationStatusEnum.Confirmed;

    public string? IdempotencyKey { get; set; }
}