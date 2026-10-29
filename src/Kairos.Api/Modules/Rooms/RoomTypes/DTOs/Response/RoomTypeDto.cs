using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Response;

public sealed class RoomTypeDto : BaseDTO
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public bool IsActive { get; init; }
}