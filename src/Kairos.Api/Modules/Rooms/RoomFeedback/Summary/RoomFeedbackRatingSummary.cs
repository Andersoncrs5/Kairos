namespace Kairos.Api.Modules.Rooms.RoomFeedback.Summary;

/// <summary>
/// Resumo das notas de uma sala. A distribuição sempre traz as chaves 1 a 5.
/// </summary>
public sealed record RoomFeedbackRatingSummary(
    int TotalCount,
    double? AverageRating,
    IReadOnlyDictionary<int, int> RatingDistribution
);

/// <summary>
/// Média de notas de uma sala (usada em listagens de salas).
/// </summary>
public sealed record RoomRatingAverage(
    double AverageRating,
    int TotalCount
);