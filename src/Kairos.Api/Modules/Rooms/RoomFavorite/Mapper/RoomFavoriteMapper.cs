using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.Mapper;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.Mapper;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class RoomFavoriteMapper
{
    public static partial RoomFavoriteDto ToDto(RoomFavoriteModel source);

    public static RoomFavoriteModel ToModel(
        string id,
        string userId,
        string roomId,
        DateTimeOffset createdAt)
    {
        return new RoomFavoriteModel
        {
            Id = id,
            UserId = userId,
            RoomId = roomId,
            Version = 1,
            CreatedAt = createdAt,
            UpdatedAt = null
        };
    }

    private static UserDTO MapUser(UserModel source)
        => UserMapper.ToDto(source);

    private static RoomDto MapRoom(RoomModel source)
        => RoomMapper.ToDto(source);
}