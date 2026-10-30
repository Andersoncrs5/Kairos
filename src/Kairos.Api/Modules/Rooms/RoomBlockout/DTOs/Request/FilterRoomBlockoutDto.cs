using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;

public sealed class FilterRoomBlockoutDto : BaseFilter
{
    public string? RoomId { get; init; }

    [EnumDataType(
        typeof(RoomBlockoutType),
        ErrorMessage = "Room blockout type is invalid.")]
    public RoomBlockoutType? Type { get; init; }

    [EnumDataType(
        typeof(RoomBlockoutStatus),
        ErrorMessage = "Room blockout status is invalid.")]
    public RoomBlockoutStatus? Status { get; init; }

    [StringLength(
        255,
        ErrorMessage = "Room blockout reason cannot exceed 255 characters.")]
    public string? Reason { get; init; }

    public bool? ReasonIgnoreCase { get; init; }

    [StringLength(
        1000,
        ErrorMessage = "Room blockout notes cannot exceed 1000 characters.")]
    public string? Notes { get; init; }

    public bool? NotesIgnoreCase { get; init; }

    public DateTimeOffset? StartsAtFrom { get; init; }

    public DateTimeOffset? StartsAtTo { get; init; }

    public DateTimeOffset? EndsAtFrom { get; init; }

    public DateTimeOffset? EndsAtTo { get; init; }

    public string? CreatedBy { get; init; }

    public string? CancelledBy { get; init; }
}