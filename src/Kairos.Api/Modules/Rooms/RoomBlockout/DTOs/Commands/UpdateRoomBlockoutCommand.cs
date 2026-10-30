using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Commands;

public sealed record UpdateRoomBlockoutCommand(
    [property: Required(
        ErrorMessage = "Room blockout ID is required.")]
    string RoomBlockoutId,

    [property: Required(
        ErrorMessage = "Room blockout update data is required.")]
    UpdateRoomBlockoutDto Dto);