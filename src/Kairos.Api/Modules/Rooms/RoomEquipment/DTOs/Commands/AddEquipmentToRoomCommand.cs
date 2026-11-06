using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Commands;

public sealed record AddEquipmentToRoomCommand(
    [property: Required(ErrorMessage = "Room ID is required.")]
    string RoomId,

    [property: Required(ErrorMessage = "Room equipment data is required.")]
    AddEquipmentToRoomDto Dto
);