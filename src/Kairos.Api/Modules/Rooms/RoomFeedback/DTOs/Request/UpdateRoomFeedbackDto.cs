using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;

/// <summary>
/// Substituição completa (PUT): Comment e IssueType nulos limpam o valor atual.
/// </summary>
public sealed class UpdateRoomFeedbackDto
{
    [Required(ErrorMessage = "Room feedback rating is required.")]
    [Range(
        1,
        5,
        ErrorMessage = "Room feedback rating must be between 1 and 5.")]
    public required int Rating { get; init; }

    [StringLength(
        1000,
        ErrorMessage = "Room feedback comment cannot exceed 1000 characters.")]
    public string? Comment { get; init; }

    [EnumDataType(
        typeof(RoomFeedbackIssueTypeEnum),
        ErrorMessage = "Room feedback issue type is invalid.")]
    public RoomFeedbackIssueTypeEnum? IssueType { get; init; }
}