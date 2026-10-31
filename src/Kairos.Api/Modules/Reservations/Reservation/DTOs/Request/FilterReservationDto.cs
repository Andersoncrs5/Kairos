using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;

public sealed class FilterReservationDto : BaseFilter
{
    public string? RoomId { get; init; }

    public string? UserId { get; init; }

    public string? CategoryId { get; init; }

    [StringLength(
        150,
        ErrorMessage = "Reservation title cannot exceed 150 characters.")]
    public string? Title { get; init; }

    public bool? TitleIgnoreCase { get; init; }

    public string? Description { get; init; }

    public bool? DescriptionIgnoreCase { get; init; }

    [EnumDataType(typeof(ReservationStatusEnum), ErrorMessage = "Reservation status is invalid.")]
    public ReservationStatusEnum? Status { get; init; }

    public DateTimeOffset? StartsAtFrom { get; init; }

    public DateTimeOffset? StartsAtTo { get; init; }

    public DateTimeOffset? EndsAtFrom { get; init; }

    public DateTimeOffset? EndsAtTo { get; init; }

    public DateTimeOffset? CancelledAtFrom { get; init; }

    public DateTimeOffset? CancelledAtTo { get; init; }

    public string? CancelledBy { get; init; }

    public DateTimeOffset? CheckedInAtFrom { get; init; }

    public DateTimeOffset? CheckedInAtTo { get; init; }

    public DateTimeOffset? CheckedOutAtFrom { get; init; }

    public DateTimeOffset? CheckedOutAtTo { get; init; }
}