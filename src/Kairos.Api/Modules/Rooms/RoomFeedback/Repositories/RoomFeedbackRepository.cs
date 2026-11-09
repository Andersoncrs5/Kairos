using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Summary;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Repositories;

public sealed class RoomFeedbackRepository(AppDbContext context)
    : BaseRepositoryImpl<RoomFeedbackModel>(context),
        IRoomFeedbackRepository
{
    private const int MinRating = 1;
    private const int MaxRating = 5;

    public Task<RoomFeedbackModel?> GetByIdWithDetailsAsync(
        string feedbackId,
        CancellationToken ct = default)
    {
        return context.Set<RoomFeedbackModel>()
            .AsNoTracking()
            .Include(feedback => feedback.Reservation)
            .Include(feedback => feedback.Room)
            .Include(feedback => feedback.User)
            .SingleOrDefaultAsync(
                feedback => feedback.Id == feedbackId,
                ct);
    }

    public Task<bool> ExistsByReservationAndUserAsync(
        string reservationId,
        string userId,
        CancellationToken ct = default)
    {
        return context.Set<RoomFeedbackModel>()
            .AsNoTracking()
            .AnyAsync(
                feedback =>
                    feedback.ReservationId == reservationId &&
                    feedback.UserId == userId,
                ct);
    }

    public async Task<RoomFeedbackRatingSummary> GetRatingSummaryByRoomIdAsync(
        string roomId,
        CancellationToken ct = default)
    {
        var counts = await context.Set<RoomFeedbackModel>()
            .AsNoTracking()
            .Where(feedback => feedback.RoomId == roomId)
            .GroupBy(feedback => feedback.Rating)
            .Select(group => new
            {
                Rating = group.Key,
                Count = group.Count()
            })
            .ToListAsync(ct);

        // Sempre devolve as chaves de 1 a 5, mesmo com zero avaliações
        Dictionary<int, int> distribution = Enumerable
            .Range(MinRating, MaxRating)
            .ToDictionary(rating => rating, _ => 0);

        foreach (var item in counts)
        {
            if (distribution.ContainsKey(item.Rating))
            {
                distribution[item.Rating] = item.Count;
            }
        }

        int totalCount = distribution.Values.Sum();

        double? averageRating = totalCount == 0
            ? null
            : distribution.Sum(pair => (double)pair.Key * pair.Value)
              / totalCount;

        return new RoomFeedbackRatingSummary(
            totalCount,
            averageRating,
            distribution);
    }

    public async Task<IReadOnlyDictionary<string, RoomRatingAverage>> GetRatingAveragesByRoomIdsAsync(
        IReadOnlyCollection<string> roomIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(roomIds);

        if (roomIds.Count == 0)
        {
            return new Dictionary<string, RoomRatingAverage>();
        }

        var rows = await context.Set<RoomFeedbackModel>()
            .AsNoTracking()
            .Where(feedback => roomIds.Contains(feedback.RoomId))
            .GroupBy(feedback => feedback.RoomId)
            .Select(group => new
            {
                RoomId = group.Key,
                Average = group.Average(feedback => feedback.Rating),
                Count = group.Count()
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            row => row.RoomId,
            row => new RoomRatingAverage(row.Average, row.Count));
    }

    public async Task<Page<RoomFeedbackModel>> Find(
        FilterRoomFeedbackDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        IQueryable<RoomFeedbackModel> query =
            context.Set<RoomFeedbackModel>()
                .AsNoTracking()
                .Include(x => x.Reservation)
                .Include(x => x.Room)
                .Include(x => x.User)
                .ApplyBaseFilter(dto);

        // ---------------------------------------------------------------
        // Identifiers
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.RoomId))
        {
            string roomId = dto.RoomId.Trim();
            query = query.Where(x => x.RoomId == roomId);
        }

        if (!string.IsNullOrWhiteSpace(dto.ReservationId))
        {
            string reservationId = dto.ReservationId.Trim();
            query = query.Where(x => x.ReservationId == reservationId);
        }

        if (!string.IsNullOrWhiteSpace(dto.UserId))
        {
            string userId = dto.UserId.Trim();
            query = query.Where(x => x.UserId == userId);
        }

        // ---------------------------------------------------------------
        // Room
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.RoomCode))
        {
            string pattern = $"%{dto.RoomCode.Trim()}%";

            query = dto.RoomCodeIgnoreCase == true
                ? query.Where(x =>
                    x.Room != null &&
                    EF.Functions.ILike(x.Room.Code, pattern))
                : query.Where(x =>
                    x.Room != null &&
                    EF.Functions.Like(x.Room.Code, pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.RoomName))
        {
            string pattern = $"%{dto.RoomName.Trim()}%";

            query = dto.RoomNameIgnoreCase == true
                ? query.Where(x =>
                    x.Room != null &&
                    EF.Functions.ILike(x.Room.Name, pattern))
                : query.Where(x =>
                    x.Room != null &&
                    EF.Functions.Like(x.Room.Name, pattern));
        }

        // ---------------------------------------------------------------
        // Author
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.UserName))
        {
            string pattern = $"%{dto.UserName.Trim()}%";

            query = dto.UserNameIgnoreCase == true
                ? query.Where(x =>
                    x.User != null &&
                    x.User.UserName != null &&
                    EF.Functions.ILike(x.User.UserName, pattern))
                : query.Where(x =>
                    x.User != null &&
                    x.User.UserName != null &&
                    EF.Functions.Like(x.User.UserName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.UserEmail))
        {
            string pattern = $"%{dto.UserEmail.Trim()}%";

            query = dto.UserEmailIgnoreCase == true
                ? query.Where(x =>
                    x.User != null &&
                    x.User.Email != null &&
                    EF.Functions.ILike(x.User.Email, pattern))
                : query.Where(x =>
                    x.User != null &&
                    x.User.Email != null &&
                    EF.Functions.Like(x.User.Email, pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.UserFullName))
        {
            string pattern = $"%{dto.UserFullName.Trim()}%";

            query = dto.UserFullNameIgnoreCase == true
                ? query.Where(x =>
                    x.User != null &&
                    x.User.FullName != null &&
                    EF.Functions.ILike(x.User.FullName, pattern))
                : query.Where(x =>
                    x.User != null &&
                    x.User.FullName != null &&
                    EF.Functions.Like(x.User.FullName, pattern));
        }

        // ---------------------------------------------------------------
        // Reservation
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.ReservationTitle))
        {
            string pattern = $"%{dto.ReservationTitle.Trim()}%";

            query = dto.ReservationTitleIgnoreCase == true
                ? query.Where(x =>
                    x.Reservation != null &&
                    EF.Functions.ILike(x.Reservation.Title, pattern))
                : query.Where(x =>
                    x.Reservation != null &&
                    EF.Functions.Like(x.Reservation.Title, pattern));
        }

        if (dto.ReservationStartsAtFrom.HasValue)
        {
            DateTimeOffset from = dto.ReservationStartsAtFrom.Value;

            query = query.Where(x =>
                x.Reservation != null &&
                x.Reservation.StartsAt >= from);
        }

        if (dto.ReservationStartsAtTo.HasValue)
        {
            DateTimeOffset to = dto.ReservationStartsAtTo.Value;

            query = query.Where(x =>
                x.Reservation != null &&
                x.Reservation.StartsAt <= to);
        }

        // ---------------------------------------------------------------
        // Rating
        // ---------------------------------------------------------------

        if (dto.RatingMin.HasValue)
        {
            int ratingMin = dto.RatingMin.Value;
            query = query.Where(x => x.Rating >= ratingMin);
        }

        if (dto.RatingMax.HasValue)
        {
            int ratingMax = dto.RatingMax.Value;
            query = query.Where(x => x.Rating <= ratingMax);
        }

        // ---------------------------------------------------------------
        // Content
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Comment))
        {
            string pattern = $"%{dto.Comment.Trim()}%";

            query = dto.CommentIgnoreCase == true
                ? query.Where(x =>
                    x.Comment != null &&
                    EF.Functions.ILike(x.Comment, pattern))
                : query.Where(x =>
                    x.Comment != null &&
                    EF.Functions.Like(x.Comment, pattern));
        }

        if (dto.IssueType.HasValue)
        {
            var issueType = dto.IssueType.Value;
            query = query.Where(x => x.IssueType == issueType);
        }

        if (dto.HasIssue.HasValue)
        {
            query = dto.HasIssue.Value
                ? query.Where(x => x.IssueType != null)
                : query.Where(x => x.IssueType == null);
        }

        // O use case grava Comment como null quando o texto é vazio,
        // então "tem comentário" é simplesmente "Comment != null".
        if (dto.HasComment.HasValue)
        {
            query = dto.HasComment.Value
                ? query.Where(x => x.Comment != null)
                : query.Where(x => x.Comment == null);
        }

        // ---------------------------------------------------------------
        // Total count
        // ---------------------------------------------------------------

        long totalCount = await query.LongCountAsync(ct);

        // ---------------------------------------------------------------
        // Pagination
        // ---------------------------------------------------------------

        int pageNumber = Math.Max(dto.Page ?? 1, 1);
        int pageSize = Math.Clamp(dto.PageSize ?? 20, 1, 100);

        // ---------------------------------------------------------------
        // Deterministic ordering (CreatedAt: o mais recente em DESC)
        // ---------------------------------------------------------------

        query = dto.ColumnOrder == ColumnOrderEnum.DESC
            ? query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
            : query
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id);

        long offset = ((long)pageNumber - 1) * pageSize;

        int skip = (int)Math.Min(offset, int.MaxValue);

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return Page<RoomFeedbackModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
}