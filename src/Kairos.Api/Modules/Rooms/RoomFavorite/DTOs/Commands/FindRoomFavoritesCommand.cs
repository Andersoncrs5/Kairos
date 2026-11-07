using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Commands;

public sealed record FindRoomFavoritesCommand(
    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(ErrorMessage = "Favorite filter is required.")]
    FilterRoomFavoriteDto Dto
);