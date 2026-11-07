namespace Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Response;

public sealed class RoomFeedbackSummaryDto
{
    public required string RoomId { get; init; }

    public required int TotalCount { get; init; }

    public double? AverageRating { get; init; }

    public required IReadOnlyDictionary<int, int> RatingDistribution { get; init; }
}