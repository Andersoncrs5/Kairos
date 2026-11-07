using Kairos.Api.Common.Base.Model;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Response;

public sealed class RoomFavoriteDto : BaseDTO
{
    public required string UserId { get; init; }
    public UserDTO? User { get; init; }

    public required string RoomId { get; init; }
    public RoomDto? Room { get; init; }
}