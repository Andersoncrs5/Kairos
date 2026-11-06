using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Commands;

public sealed record UpdateRoomEquipmentCommand(
    [property: Required(ErrorMessage = "Room equipment association ID is required.")]
    string RoomEquipmentId,

    [property: Required(ErrorMessage = "Room equipment update data is required.")]
    UpdateRoomEquipmentDto Dto
);