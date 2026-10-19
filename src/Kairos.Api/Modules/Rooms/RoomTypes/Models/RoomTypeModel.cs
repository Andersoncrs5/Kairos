using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Models;

public class RoomTypeModel : BaseModel
{
    public required string Name { get; set; }

    public string? Description { get; set; }
    
    public bool IsActive { get; set; }
}