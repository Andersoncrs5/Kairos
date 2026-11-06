using Kairos.Api.Common.Base.Model;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Models;

public sealed class RoomEquipmentModel : BaseModel
{
    public required string RoomId { get; set; }
    public RoomModel? Room { get; set; }

    public required string EquipmentId { get; set; }
    public EquipmentModel? Equipment { get; set; }

    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
}