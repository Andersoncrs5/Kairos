using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Request;

public sealed class FilterRoomFavoriteDto : BaseFilter
{
    public string? RoomId { get; init; }

    public string? UserId { get; set; }

    [StringLength(30, ErrorMessage = "Room code cannot exceed 30 characters.")]
    public string? CodeRoom { get; init; }

    public bool? CodeRoomIgnoreCase { get; init; }

    [StringLength(120, ErrorMessage = "Room name cannot exceed 120 characters.")]
    public string? NameRoom { get; init; }

    public bool? NameRoomIgnoreCase { get; init; }
}