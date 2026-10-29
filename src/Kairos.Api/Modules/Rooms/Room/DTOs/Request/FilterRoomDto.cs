using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.Room.DTOs.Request;

public sealed class FilterRoomDto : BaseFilter
{
    public string? RoomTypeId { get; init; }
    [StringLength(100, ErrorMessage = "Room name cannot exceed 100 characters.")]
    public string? NameRoomType { get; init; }

    public bool? NameRoomTypeIgnoreCase { get; init; }
    
    [StringLength(30, ErrorMessage = "Room code cannot exceed 30 characters.")]
    public string? Code { get; init; }

    public bool? CodeIgnoreCase { get; init; }

    [StringLength(120, ErrorMessage = "Room name cannot exceed 120 characters.")]
    public string? Name { get; init; }

    public bool? NameIgnoreCase { get; init; }

    [StringLength(500, ErrorMessage = "Room description cannot exceed 500 characters.")]
    public string? Description { get; init; }

    public bool? DescriptionIgnoreCase { get; init; }

    [StringLength(150, ErrorMessage = "Room location cannot exceed 150 characters.")]
    public string? Location { get; init; }

    public bool? LocationIgnoreCase { get; init; }

    [StringLength(
        30,
        ErrorMessage = "Room floor cannot exceed 30 characters.")]
    public string? Floor { get; init; }

    public bool? FloorIgnoreCase { get; init; }

    [Range(1, int.MaxValue)]
    public int? MinCapacity { get; init; }

    [Range(1, int.MaxValue)]
    public int? MaxCapacity { get; init; }
    
    public decimal? AreaSquareMetersMin { get; set; }
    
    public decimal? AreaSquareMetersMax { get; set; }

    public bool? IsAccessible { get; init; }

    public bool? IsReservable { get; init; }

    public bool? IsActive { get; init; }
}