using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;

public sealed class FilterRoomFeedbackDto : BaseFilter, IValidatableObject
{
    
    public string? RoomId { get; init; }

    public string? ReservationId { get; init; }

    public string? UserId { get; set; }

    [StringLength(30, ErrorMessage = "Room code cannot exceed 30 characters.")]
    public string? RoomCode { get; init; }

    public bool? RoomCodeIgnoreCase { get; init; }

    [StringLength(120, ErrorMessage = "Room name cannot exceed 120 characters.")]
    public string? RoomName { get; init; }

    public bool? RoomNameIgnoreCase { get; init; }

    
    [StringLength(256, ErrorMessage = "User name cannot exceed 256 characters.")]
    public string? UserName { get; init; }

    public bool? UserNameIgnoreCase { get; init; }

    [StringLength(256, ErrorMessage = "User email cannot exceed 256 characters.")]
    public string? UserEmail { get; init; }

    public bool? UserEmailIgnoreCase { get; init; }

    [StringLength(256, ErrorMessage = "User full name cannot exceed 256 characters.")]
    public string? UserFullName { get; init; }

    public bool? UserFullNameIgnoreCase { get; init; }
    
    [StringLength(
        150,
        ErrorMessage = "Reservation title cannot exceed 150 characters.")]
    public string? ReservationTitle { get; init; }

    public bool? ReservationTitleIgnoreCase { get; init; }

    public DateTimeOffset? ReservationStartsAtFrom { get; init; }
    public DateTimeOffset? ReservationStartsAtTo { get; init; }
    
    [Range(1, 5, ErrorMessage = "Minimum rating must be between 1 and 5.")]
    public int? RatingMin { get; init; }

    [Range(1, 5, ErrorMessage = "Maximum rating must be between 1 and 5.")]
    public int? RatingMax { get; init; }

    [StringLength(1000, ErrorMessage = "Room feedback comment cannot exceed 1000 characters.")]
    public string? Comment { get; init; }

    public bool? CommentIgnoreCase { get; init; }

    [EnumDataType(
        typeof(RoomFeedbackIssueTypeEnum),
        ErrorMessage = "Room feedback issue type is invalid.")]
    public RoomFeedbackIssueTypeEnum? IssueType { get; init; }

    // true: só avaliações que relataram algum problema; false: só as sem problema
    public bool? HasIssue { get; init; }

    // true: só avaliações com comentário; false: só as sem comentário
    public bool? HasComment { get; init; }

    // ---------------------------------------------------------------
    // Validação
    // ---------------------------------------------------------------

    // O Validate do BaseFilter não é virtual, então reimplemento a interface
    // e chamo o base para manter as validações de intervalo dele.
    public new IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        foreach (ValidationResult result in base.Validate(validationContext))
        {
            yield return result;
        }

        foreach (ValidationResult result in ValidateRange(
                     RatingMin,
                     RatingMax,
                     nameof(RatingMin),
                     nameof(RatingMax)))
        {
            yield return result;
        }

        foreach (ValidationResult result in ValidateRange(
                     ReservationStartsAtFrom,
                     ReservationStartsAtTo,
                     nameof(ReservationStartsAtFrom),
                     nameof(ReservationStartsAtTo)))
        {
            yield return result;
        }

        // "Sem problema" e "um tipo de problema específico" se contradizem
        if (HasIssue == false && IssueType.HasValue)
        {
            yield return new ValidationResult(
                "IssueType cannot be combined with HasIssue = false.",
                [nameof(IssueType), nameof(HasIssue)]);
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