using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Commands;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.Services.Interfaces;

public interface IRemoveRoomFavoriteUseCase
    : IUseCase<RemoveRoomFavoriteCommand, Result<bool>>
{
}