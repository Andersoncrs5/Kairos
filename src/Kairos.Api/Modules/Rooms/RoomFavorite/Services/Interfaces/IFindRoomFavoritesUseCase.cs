using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.Services.Interfaces;

public interface IFindRoomFavoritesUseCase
    : IUseCase<FindRoomFavoritesCommand, Result<Page<RoomFavoriteModel>>>
{
}