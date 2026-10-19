using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Models;

public class RoomBlockoutModel : BaseModel
{
    public required string RoomId { get; set; }

    public required string Reason { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public required string CreatedBy { get; set; }
}