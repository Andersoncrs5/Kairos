using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.Room.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.Room.DTOs.Commands;

public sealed record UpdateRoomCommand(
    [property: Required(ErrorMessage = "Room ID is required.")]
    string RoomId,

    [property: Required(ErrorMessage = "Room update data is required.")]
    UpdateRoomDto Dto);