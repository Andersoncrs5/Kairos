using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Commands;

public sealed record UpdateRoomTypeCommand(
    [property: Required(ErrorMessage = "Room type ID is required.")]
    string RoomTypeId,

    [property: Required(ErrorMessage = "Room type update data is required.")]
    UpdateRoomTypeDto Dto
);