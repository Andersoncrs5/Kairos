using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;

public sealed class UpdateRoomBlockoutDto
{
    public string? RoomId { get; init; }

    [EnumDataType(
        typeof(RoomBlockoutType),
        ErrorMessage = "Room blockout type is invalid.")]
    public RoomBlockoutType? Type { get; init; }

    [StringLength(
        255,
        ErrorMessage = "Room blockout reason cannot exceed 255 characters.")]
    public string? Reason { get; init; }

    [StringLength(
        1000,
        ErrorMessage = "Room blockout notes cannot exceed 1000 characters.")]
    public string? Notes { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }
}