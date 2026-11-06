using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.Equipment.DTOs.Response;

public sealed class EquipmentDto : BaseDTO
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
}