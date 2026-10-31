using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Reservations.Reservation.Repositories;

public sealed class ReservationRepository(AppDbContext context)
    : BaseRepositoryImpl<ReservationModel>(context),
        IReservationRepository
{
    public Task<ReservationModel?> GetByIdempotencyKeyAsync(string userId, string idempotencyKey, CancellationToken ct = default)
    {
        return context.Set<ReservationModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                reservation =>
                    reservation.UserId == userId &&
                    reservation.IdempotencyKey == idempotencyKey,
                ct);
    }

    public async Task<IReadOnlyList<ReservationModel>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        return await context.Set<ReservationModel>()
            .AsNoTracking()
            .Where(reservation => reservation.UserId == userId)
            .OrderByDescending(reservation => reservation.StartsAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReservationModel>> GetConflictingByRoomAsync(string roomId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken ct = default)
    {
        return await context.Set<ReservationModel>()
            .AsNoTracking()
            .Where(reservation =>
                reservation.RoomId == roomId &&
                reservation.Status != ReservationStatusEnum.Cancelled &&
                reservation.StartsAt < endsAt &&
                reservation.EndsAt > startsAt)
            .OrderBy(reservation => reservation.StartsAt)
            .ToListAsync(ct);
    }
    
    public Task<bool> ExistsConflictingByRoomAsync(string roomId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken ct = default)
    {
        return context.Set<ReservationModel>()
            .AsNoTracking()
            .AnyAsync(
                reservation =>
                    reservation.RoomId == roomId &&
                    reservation.Status != ReservationStatusEnum.Cancelled &&
                    reservation.StartsAt < endsAt &&
                    reservation.EndsAt > startsAt,
                ct);
    }
    
    public async Task<IReadOnlyList<ReservationModel>> GetUpcomingByUserIdAsync(string userId, DateTimeOffset from, int take = 10, CancellationToken ct = default)
    {
        return await context.Set<ReservationModel>()
            .AsNoTracking()
            .Where(reservation =>
                reservation.UserId == userId &&
                reservation.StartsAt >= from &&
                reservation.Status != ReservationStatusEnum.Cancelled)
            .OrderBy(reservation => reservation.StartsAt)
            .Take(take)
            .ToListAsync(ct);
    }
    
    public async Task<Page<ReservationModel>> Find(
        FilterReservationDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        IQueryable<ReservationModel> query = context.Set<ReservationModel>()
            .AsNoTracking()
            .Include(x => x.Room)
            .Include(x => x.User)
            .Include(x => x.Category)
            .Include(x => x.Canceller);

        // ---------------------------------------------------------------
        // Identifiers and base filters
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Id))
            query = query.Where(x => x.Id == dto.Id.Trim());

        if (dto.VersionMin.HasValue)
            query = query.Where(x => x.Version >= dto.VersionMin.Value);

        if (dto.VersionMax.HasValue)
            query = query.Where(x => x.Version <= dto.VersionMax.Value);

        if (dto.CreatedAtMin.HasValue)
            query = query.Where(x => x.CreatedAt >= dto.CreatedAtMin.Value);

        if (dto.CreatedAtMax.HasValue)
            query = query.Where(x => x.CreatedAt <= dto.CreatedAtMax.Value);

        if (dto.UpdatedAtMin.HasValue)
        {
            query = query.Where(x =>
                x.UpdatedAt.HasValue &&
                x.UpdatedAt.Value >= dto.UpdatedAtMin.Value);
        }

        if (dto.UpdatedAtMax.HasValue)
        {
            query = query.Where(x =>
                x.UpdatedAt.HasValue &&
                x.UpdatedAt.Value <= dto.UpdatedAtMax.Value);
        }

        // ---------------------------------------------------------------
        // Room
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.RoomId))
        {
            query = query.Where(x =>
                x.RoomId == dto.RoomId.Trim());
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

        // ---------------------------------------------------------------
        // User
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.UserId))
        {
            query = query.Where(x =>
                x.UserId == dto.UserId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(dto.UserUserName))
        {
            string pattern = $"%{dto.UserUserName.Trim()}%";

            query = dto.UserIgnoreCaseUserName == true
                ? query.Where(x =>
                    x.User != null &&
                    x.User.UserName != null &&
                    EF.Functions.ILike(x.User.UserName, pattern))
                : query.Where(x =>
                    x.User != null &&
                    x.User.UserName != null &&
                    EF.Functions.Like(x.User.UserName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            string pattern = $"%{dto.Email.Trim()}%";

            query = dto.IgnoreCaseEmail == true
                ? query.Where(x =>
                    x.User != null &&
                    x.User.Email != null &&
                    EF.Functions.ILike(x.User.Email, pattern))
                : query.Where(x =>
                    x.User != null &&
                    x.User.Email != null &&
                    EF.Functions.Like(x.User.Email, pattern));
        }

        // ---------------------------------------------------------------
        // Category
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.CategoryId))
        {
            query = query.Where(x =>
                x.CategoryId == dto.CategoryId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(dto.CategoryName))
        {
            string pattern = $"%{dto.CategoryName.Trim()}%";

            query = dto.CategoryNameIgnoreCase == true
                ? query.Where(x =>
                    x.Category != null &&
                    EF.Functions.ILike(x.Category.Name, pattern))
                : query.Where(x =>
                    x.Category != null &&
                    EF.Functions.Like(x.Category.Name, pattern));
        }

        // ---------------------------------------------------------------
        // Title
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Title))
        {
            string pattern = $"%{dto.Title.Trim()}%";

            query = dto.TitleIgnoreCase == true
                ? query.Where(x =>
                    EF.Functions.ILike(x.Title, pattern))
                : query.Where(x =>
                    EF.Functions.Like(x.Title, pattern));
        }

        // ---------------------------------------------------------------
        // Description
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.Description))
        {
            string pattern = $"%{dto.Description.Trim()}%";

            query = dto.DescriptionIgnoreCase == true
                ? query.Where(x =>
                    x.Description != null &&
                    EF.Functions.ILike(x.Description, pattern))
                : query.Where(x =>
                    x.Description != null &&
                    EF.Functions.Like(x.Description, pattern));
        }

        // ---------------------------------------------------------------
        // Status
        // ---------------------------------------------------------------

        if (dto.Status.HasValue)
        {
            query = query.Where(x =>
                x.Status == dto.Status.Value);
        }

        // ---------------------------------------------------------------
        // Reservation period
        // ---------------------------------------------------------------

        if (dto.StartsAtFrom.HasValue)
        {
            query = query.Where(x =>
                x.StartsAt >= dto.StartsAtFrom.Value);
        }

        if (dto.StartsAtTo.HasValue)
        {
            query = query.Where(x =>
                x.StartsAt <= dto.StartsAtTo.Value);
        }

        if (dto.EndsAtFrom.HasValue)
        {
            query = query.Where(x =>
                x.EndsAt >= dto.EndsAtFrom.Value);
        }

        if (dto.EndsAtTo.HasValue)
        {
            query = query.Where(x =>
                x.EndsAt <= dto.EndsAtTo.Value);
        }

        // ---------------------------------------------------------------
        // Cancellation
        // ---------------------------------------------------------------

        if (dto.CancelledAtFrom.HasValue)
        {
            query = query.Where(x =>
                x.CancelledAt.HasValue &&
                x.CancelledAt.Value >= dto.CancelledAtFrom.Value);
        }

        if (dto.CancelledAtTo.HasValue)
        {
            query = query.Where(x =>
                x.CancelledAt.HasValue &&
                x.CancelledAt.Value <= dto.CancelledAtTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(dto.CancelledBy))
        {
            query = query.Where(x =>
                x.CancelledBy == dto.CancelledBy.Trim());
        }

        if (!string.IsNullOrWhiteSpace(dto.CancelledUserName))
        {
            string pattern = $"%{dto.CancelledUserName.Trim()}%";

            query = dto.CancelledIgnoreCaseUserName == true
                ? query.Where(x =>
                    x.Canceller != null &&
                    x.Canceller.UserName != null &&
                    EF.Functions.ILike(x.Canceller.UserName, pattern))
                : query.Where(x =>
                    x.Canceller != null &&
                    x.Canceller.UserName != null &&
                    EF.Functions.Like(x.Canceller.UserName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(dto.CancelledEmail))
        {
            string pattern = $"%{dto.CancelledEmail.Trim()}%";

            query = dto.CancelledIgnoreCaseEmail == true
                ? query.Where(x =>
                    x.Canceller != null &&
                    x.Canceller.Email != null &&
                    EF.Functions.ILike(x.Canceller.Email, pattern))
                : query.Where(x =>
                    x.Canceller != null &&
                    x.Canceller.Email != null &&
                    EF.Functions.Like(x.Canceller.Email, pattern));
        }

        // ---------------------------------------------------------------
        // Check-in
        // ---------------------------------------------------------------

        if (dto.CheckedInAtFrom.HasValue)
        {
            query = query.Where(x =>
                x.CheckedInAt.HasValue &&
                x.CheckedInAt.Value >= dto.CheckedInAtFrom.Value);
        }

        if (dto.CheckedInAtTo.HasValue)
        {
            query = query.Where(x =>
                x.CheckedInAt.HasValue &&
                x.CheckedInAt.Value <= dto.CheckedInAtTo.Value);
        }

        // ---------------------------------------------------------------
        // Check-out
        // ---------------------------------------------------------------

        if (dto.CheckedOutAtFrom.HasValue)
        {
            query = query.Where(x =>
                x.CheckedOutAt.HasValue &&
                x.CheckedOutAt.Value >= dto.CheckedOutAtFrom.Value);
        }

        if (dto.CheckedOutAtTo.HasValue)
        {
            query = query.Where(x =>
                x.CheckedOutAt.HasValue &&
                x.CheckedOutAt.Value <= dto.CheckedOutAtTo.Value);
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
        // Deterministic ordering
        // ---------------------------------------------------------------

        query = dto.ColumnOrder == ColumnOrderEnum.DESC
            ? query
                .OrderByDescending(x => x.StartsAt)
                .ThenByDescending(x => x.Id)
            : query
                .OrderBy(x => x.StartsAt)
                .ThenBy(x => x.Id);

        long offset = ((long)pageNumber - 1) * pageSize;

        int skip = (int)Math.Min(offset, int.MaxValue);

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return Page<ReservationModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
    
    public Task<bool> ExistsConflictingByRoomAsync(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string excludeReservationId,
        CancellationToken ct = default)
    {
        return context.Set<ReservationModel>()
            .AsNoTracking()
            .AnyAsync(
                reservation =>
                    reservation.Id != excludeReservationId &&
                    reservation.RoomId == roomId &&
                    reservation.Status != ReservationStatusEnum.Cancelled &&
                    reservation.StartsAt < endsAt &&
                    reservation.EndsAt > startsAt,
                ct);
    }
    
}