using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;

public sealed class UpdateReservationAttachmentDto
{
    [Required(ErrorMessage = "Reservation attachment display name is required.")]
    [StringLength(
        255,
        MinimumLength = 1,
        ErrorMessage = "Reservation attachment display name must contain between 1 and 255 characters.")]
    public required string DisplayName { get; init; }

    [StringLength(
        500,
        ErrorMessage = "Reservation attachment description cannot exceed 500 characters.")]
    public string? Description { get; init; }

    [EnumDataType(
        typeof(ReservationAttachmentVisibilityEnum),
        ErrorMessage = "Reservation attachment visibility is invalid.")]
    public ReservationAttachmentVisibilityEnum Visibility { get; init; }
}