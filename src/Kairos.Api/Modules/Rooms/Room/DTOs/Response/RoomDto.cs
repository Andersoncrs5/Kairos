using Kairos.Api.Common.Base.Model;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Response;

namespace Kairos.Api.Modules.Rooms.Room.DTOs.Response;

public sealed class RoomDto : BaseDTO
{
    public required string RoomTypeId { get; init; }
    public RoomTypeDto? RoomTypeDto { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string Location { get; init; }

    public string? Floor { get; init; }

    public int Capacity { get; init; }

    public decimal? AreaSquareMeters { get; init; }

    public bool IsAccessible { get; init; }

    public bool IsReservable { get; init; }

    public bool IsActive { get; init; }
}