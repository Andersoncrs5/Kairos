using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Commands;

public sealed record CreateRoomOperatingHourCommand(
    [property: Required(ErrorMessage = "Room ID is required.")]
    string RoomId,

    [property: Required(ErrorMessage = "Room operating hour data is required.")]
    CreateRoomOperatingHourDto Dto
);