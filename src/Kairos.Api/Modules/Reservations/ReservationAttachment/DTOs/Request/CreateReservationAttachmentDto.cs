using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;

public sealed class CreateReservationAttachmentDto : IValidatableObject
{
    [Required(ErrorMessage = "Reservation attachment file is required.")]
    public required IFormFile File { get; init; }

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
        = ReservationAttachmentVisibilityEnum.Participants;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (File is null)
            yield break;

        if (File.Length <= 0)
        {
            yield return new ValidationResult(
                "Reservation attachment file cannot be empty.",
                [nameof(File)]);
        }

        if (string.IsNullOrWhiteSpace(File.FileName))
        {
            yield return new ValidationResult(
                "Reservation attachment file name is required.",
                [nameof(File)]);
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            yield return new ValidationResult(
                "Reservation attachment display name cannot be empty.",
                [nameof(DisplayName)]);
        }

        if (!Enum.IsDefined(Visibility))
        {
            yield return new ValidationResult(
                "Reservation attachment visibility is invalid.",
                [nameof(Visibility)]);
        }
    }
}