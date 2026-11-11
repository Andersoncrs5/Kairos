using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;
using Microsoft.AspNetCore.Http;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;

public sealed class CreateRoomAttachmentDto : IValidatableObject
{
    [Required(ErrorMessage = "Room attachment file is required.")]
    public required IFormFile File { get; init; }

    [EnumDataType(
        typeof(RoomAttachmentTypeEnum),
        ErrorMessage = "Room attachment type is invalid.")]
    public RoomAttachmentTypeEnum Type { get; init; }
        = RoomAttachmentTypeEnum.Photo;

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

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (File is null)
            yield break;

        if (File.Length <= 0)
        {
            yield return new ValidationResult(
                "Room attachment file cannot be empty.",
                [nameof(File)]);
        }

        if (string.IsNullOrWhiteSpace(File.FileName))
        {
            yield return new ValidationResult(
                "Room attachment file name is required.",
                [nameof(File)]);
        }

        if (!Enum.IsDefined(Type))
        {
            yield return new ValidationResult(
                "Room attachment type is invalid.",
                [nameof(Type)]);
        }

        if (SortOrder < 0)
        {
            yield return new ValidationResult(
                "Room attachment sort order cannot be negative.",
                [nameof(SortOrder)]);
        }

        if (IsCover && Type != RoomAttachmentTypeEnum.Photo)
        {
            yield return new ValidationResult(
                "Only photo attachments can be marked as cover.",
                [nameof(IsCover), nameof(Type)]);
        }
    }
}