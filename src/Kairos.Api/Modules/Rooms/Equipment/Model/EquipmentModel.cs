using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.Equipment.Model;

public class EquipmentModel: BaseModel
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}