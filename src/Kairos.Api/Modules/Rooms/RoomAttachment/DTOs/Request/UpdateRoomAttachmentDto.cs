using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;

public sealed class UpdateRoomAttachmentDto
{
    [StringLength(
        255,
        ErrorMessage = "Room attachment alt text cannot exceed 255 characters.")]
    public string? AltText { get; init; }

    [Range(
        0,
        int.MaxValue,
        ErrorMessage = "Sort order cannot be negative.")]
    public int SortOrder { get; init; }

    public bool IsCover { get; init; }
}