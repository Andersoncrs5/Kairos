using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Categories.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;

namespace Kairos.Api.Modules.Reservations.Reservation.DTOs.Response;

public sealed class ReservationDto : BaseDTO
{
    public required string RoomId { get; init; }

    public RoomDto? RoomDto { get; init; }

    public required string UserId { get; init; }

    public UserDTO? User { get; init; }

    public required string CategoryId { get; init; }
    public CategoryDto? CategoryDto { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    public required ReservationStatusEnum Status { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public string? CancellationReason { get; init; }

    public string? CancelledBy { get; init; }

    public UserDTO? Canceller { get; init; }

    public DateTimeOffset? CheckedInAt { get; init; }

    public DateTimeOffset? CheckedOutAt { get; init; }
}