using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Commands;

public sealed record UpdateRoomOperatingHourCommand(
    [property: Required(ErrorMessage = "Room operating hour ID is required.")]
    string RoomOperatingHourId,

    [property: Required(ErrorMessage = "Room operating hour update data is required.")]
    UpdateRoomOperatingHourDto Dto
);