using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Response;

public sealed class RoomBlockoutDto : BaseDTO
{
    public required string RoomId { get; init; }
    public RoomDto? RoomDto { get; init; }

    public required RoomBlockoutType Type { get; init; }

    public required RoomBlockoutStatus Status { get; init; }

    public required string Reason { get; init; }

    public string? Notes { get; init; }

    public DateTimeOffset StartsAt { get; init; }

    public DateTimeOffset EndsAt { get; init; }

    public DateTimeOffset? CancelledAt { get; init; }

    public string? CancellationReason { get; init; }

    public string? CancelledBy { get; init; }
    public UserDTO? Canceller {  get; init; }

    public required string CreatedBy { get; init; }
    public UserDTO? Creater {  get; init; }
}