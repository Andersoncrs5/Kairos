using Kairos.Api.Common.Base.Model;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;

namespace Kairos.Api.Modules.Rooms.Room.Models;

public class RoomModel : BaseModel
{
    public required string RoomTypeId { get; set; }

    public RoomTypeModel? RoomTypeModel { get; set; }

    public required string Code { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public required string Location { get; set; }

    public string? Floor { get; set; }

    public int Capacity { get; set; }

    public decimal? AreaSquareMeters { get; set; }

    public bool IsAccessible { get; set; }

    public bool IsReservable { get; set; } = true;

    public bool IsActive { get; set; } = true;
}