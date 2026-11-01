using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Response;

public sealed class RoomOperatingHourDto : BaseDTO
{
    public required string RoomId { get; init; }

    public DayOfWeek DayOfWeek { get; init; }

    public TimeOnly OpensAt { get; init; }

    public TimeOnly ClosesAt { get; init; }

    public bool IsClosed { get; init; }

    public bool IsActive { get; init; }

    public string? Notes { get; init; }
}