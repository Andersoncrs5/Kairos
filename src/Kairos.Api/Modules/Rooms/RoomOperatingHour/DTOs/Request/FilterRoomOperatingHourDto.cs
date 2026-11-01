using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;

public sealed class FilterRoomOperatingHourDto : BaseFilter
{
    [StringLength(36, ErrorMessage = "Room ID cannot exceed 36 characters.")]
    public string? RoomId { get; init; }
    [StringLength(100, ErrorMessage = "Room type name cannot exceed 100 characters.")]
    public string? RoomName { get; init; }
    public bool? RoomNameIgnoreCase { get; init; }
    
    public DayOfWeek? DayOfWeek { get; init; }

    public TimeOnly? OpensAtFrom { get; init; }
    public TimeOnly? OpensAtTo { get; init; }
    
    public TimeOnly? ClosesAtFrom { get; init; }
    public TimeOnly? ClosesAtTo { get; init; }

    public bool? IsClosed { get; init; }

    public bool? IsActive { get; init; }
}