using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;

public sealed class FilterRoomAttachmentDto : BaseFilter, IValidatableObject
{
    public string? RoomId { get; set; }

    public RoomAttachmentTypeEnum? Type { get; init; }

    public string? CreatedBy { get; init; }

    public string? CreatorUserName { get; init; }

    public bool? IgnoreCaseCreatorUserName { get; init; }

    public string? CreatorEmail { get; init; }

    public bool? IgnoreCaseCreatorEmail { get; init; }

    public string? CreatorFullName { get; init; }

    public bool? IgnoreCaseCreatorFullName { get; init; }

    public string? CreatorPhoneNumber { get; init; }

    public bool? IgnoreCaseCreatorPhoneNumber { get; init; }

    [StringLength(
        255,
        ErrorMessage = "Original file name cannot exceed 255 characters.")]
    public string? OriginalFileName { get; init; }

    public bool? OriginalFileNameIgnoreCase { get; init; }

    [StringLength(
        100,
        ErrorMessage = "Content type cannot exceed 100 characters.")]
    public string? ContentType { get; init; }

    [StringLength(
        255,
        ErrorMessage = "Alt text cannot exceed 255 characters.")]
    public string? AltText { get; init; }

    public bool? AltTextIgnoreCase { get; init; }

    public bool? IsCover { get; init; }

    public int? MinSortOrder { get; init; }

    public int? MaxSortOrder { get; init; }

    public DateTimeOffset? CreatedAtFrom { get; init; }

    public DateTimeOffset? CreatedAtTo { get; init; }

    public long? MinSizeBytes { get; init; }

    public long? MaxSizeBytes { get; init; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        foreach (ValidationResult result in base.Validate(validationContext))
            yield return result;

        if (Type.HasValue && !Enum.IsDefined(Type.Value))
        {
            yield return new ValidationResult(
                "Room attachment type is invalid.",
                [nameof(Type)]);
        }

        if (MinSortOrder < 0)
        {
            yield return new ValidationResult(
                "Minimum sort order cannot be negative.",
                [nameof(MinSortOrder)]);
        }

        if (MaxSortOrder < 0)
        {
            yield return new ValidationResult(
                "Maximum sort order cannot be negative.",
                [nameof(MaxSortOrder)]);
        }

        if (MinSizeBytes < 0)
        {
            yield return new ValidationResult(
                "Minimum file size cannot be negative.",
                [nameof(MinSizeBytes)]);
        }

        if (MaxSizeBytes < 0)
        {
            yield return new ValidationResult(
                "Maximum file size cannot be negative.",
                [nameof(MaxSizeBytes)]);
        }

        foreach (ValidationResult result in ValidateRange(
                     CreatedAtFrom,
                     CreatedAtTo,
                     nameof(CreatedAtFrom),
                     nameof(CreatedAtTo)))
        {
            yield return result;
        }

        foreach (ValidationResult result in ValidateRange(
                     MinSizeBytes,
                     MaxSizeBytes,
                     nameof(MinSizeBytes),
                     nameof(MaxSizeBytes)))
        {
            yield return result;
        }

        foreach (ValidationResult result in ValidateRange(
                     MinSortOrder,
                     MaxSortOrder,
                     nameof(MinSortOrder),
                     nameof(MaxSortOrder)))
        {
            yield return result;
        }
    }

    private static IEnumerable<ValidationResult> ValidateRange<T>(
        T? min,
        T? max,
        string minName,
        string maxName)
        where T : struct, IComparable<T>
    {
        if (min.HasValue &&
            max.HasValue &&
            min.Value.CompareTo(max.Value) > 0)
        {
            yield return new ValidationResult(
                $"{minName} cannot be greater than {maxName}.",
                [minName, maxName]);
        }
    }
}