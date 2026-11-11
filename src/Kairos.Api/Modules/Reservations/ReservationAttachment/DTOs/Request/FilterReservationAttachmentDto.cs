using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;

public sealed class FilterReservationAttachmentDto : BaseFilter, IValidatableObject
{
    public string? ReservationId { get; set; }
    public string? ReservationTitle { get; set; }
    public bool? ReservationTitleIgnoreCase { get; init; }
    
    public string? UploadedBy { get; init; }
    public string? UploadedUserName { get; set; }
    public bool? IgnoreCaseUploadedUserName { get; set; }

    public string? UploadedEmail { get; set; }
    public bool? IgnoreCaseUploadedEmail { get; set; }
    
    public string? UploadedFullName { get; set; }
    public bool? IgnoreCaseUploadedFullName { get; set; }
    
    public string? UploadedPhoneNumber { get; set; }
    public bool? IgnoreCaseUploadedPhoneNumber { get; set; }

    
    [StringLength(255, ErrorMessage = "Original file name cannot exceed 255 characters.")]
    public string? OriginalFileName { get; init; }
    public bool? OriginalFileNameIgnoreCase { get; init; }

    [StringLength(
        255,
        ErrorMessage = "Display name cannot exceed 255 characters.")]
    public string? DisplayName { get; init; }

    public bool? DisplayNameIgnoreCase { get; init; }

    [StringLength(
        100,
        ErrorMessage = "Content type cannot exceed 100 characters.")]
    public string? ContentType { get; init; }

    public ReservationAttachmentVisibilityEnum? Visibility { get; init; }

    public FileScanStatusEnum? ScanStatus { get; init; }

    public DateTimeOffset? CreatedAtFrom { get; init; }

    public DateTimeOffset? CreatedAtTo { get; init; }

    public long? MinSizeBytes { get; init; }

    public long? MaxSizeBytes { get; init; }

    // true: somente anexos com descrição.
    // false: somente anexos sem descrição.
    public bool? HasDescription { get; init; }

    public new IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        foreach (ValidationResult result in base.Validate(validationContext))
            yield return result;

        if (Visibility.HasValue &&
            !Enum.IsDefined(Visibility.Value))
        {
            yield return new ValidationResult(
                "Reservation attachment visibility is invalid.",
                [nameof(Visibility)]);
        }

        if (ScanStatus.HasValue &&
            !Enum.IsDefined(ScanStatus.Value))
        {
            yield return new ValidationResult(
                "Reservation attachment scan status is invalid.",
                [nameof(ScanStatus)]);
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