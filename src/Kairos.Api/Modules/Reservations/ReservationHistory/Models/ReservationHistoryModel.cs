using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Reservations.ReservationHistory.Models;

public class ReservationHistoryModel : BaseModel
{
    public required string ReservationId { get; set; }

    public required string ActorUserId { get; set; }

    public required string Action { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }
}