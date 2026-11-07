using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Commands;

public sealed record RemoveRoomFavoriteCommand(
    [property: Required(ErrorMessage = "Room ID is required.")]
    string RoomId,

    [property: Required(ErrorMessage = "User ID is required.")]
    string UserId
);