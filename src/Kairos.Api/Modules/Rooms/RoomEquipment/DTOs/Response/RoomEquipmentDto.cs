using Kairos.Api.Common.Base.Model;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Response;

public sealed class RoomEquipmentDto: BaseDTO
{
    public required string RoomId { get; init; }
    public RoomDto? Room { get; init; }
    
    public required string EquipmentId { get; init; }
    public EquipmentDto? Equipment { get; init; }
    
    public int Quantity { get; init; }
    public string? Notes { get; init; }
}