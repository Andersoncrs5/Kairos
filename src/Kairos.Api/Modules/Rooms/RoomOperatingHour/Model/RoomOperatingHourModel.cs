using Kairos.Api.Common.Base.Model;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;

public class RoomOperatingHourModel : BaseModel
{
    public required string RoomId { get; set; }
    public RoomModel? Room { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly OpensAt { get; set; }

    public TimeOnly ClosesAt { get; set; }

    public bool IsClosed { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }
}