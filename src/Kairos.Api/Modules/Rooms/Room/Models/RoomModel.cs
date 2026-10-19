using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.Room.Models;

public class RoomModel : BaseModel
{
    public required string RoomTypeId { get; set; }

    public required string Name { get; set; }

    public required string Location { get; set; }

    public int Capacity { get; set; }

    public bool IsActive { get; set; } = true;
}
