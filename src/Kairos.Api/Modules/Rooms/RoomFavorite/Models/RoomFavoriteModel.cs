using Kairos.Api.Common.Base.Model;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.Models;

public class RoomFavoriteModel : BaseModel
{
    public required string UserId { get; set; }
    public UserModel? User { get; set; }

    public required string RoomId { get; set; }
    public RoomModel? Room { get; set; }
}