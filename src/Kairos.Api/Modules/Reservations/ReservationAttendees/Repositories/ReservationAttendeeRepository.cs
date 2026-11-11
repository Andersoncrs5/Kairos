using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Page;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Repositories;

public sealed class ReservationAttendeeRepository(AppDbContext context)
    : BaseRepositoryImpl<ReservationAttendeeModel>(context),
        IReservationAttendeeRepository
{
    public Task<ReservationAttendeeModel?> GetByReservationAndUserAsync(
        string reservationId,
        string userId,
        CancellationToken ct = default)
    {
        return context.ReservationAttendees
            .SingleOrDefaultAsync(
                attendee =>
                    attendee.ReservationId == reservationId &&
                    attendee.UserId == userId,
                ct);
    }


    public Task<ReservationAttendeeModel?> GetByIdAndReservationAsync(
        string attendeeId,
        string reservationId,
        CancellationToken ct = default)
    {
        return context.ReservationAttendees
            .SingleOrDefaultAsync(
                attendee =>
                    attendee.Id == attendeeId &&
                    attendee.ReservationId == reservationId,
                ct);
    }

    public async Task<IReadOnlyList<ReservationAttendeeModel>> GetByReservationIdAsync(
        string reservationId,
        CancellationToken ct = default)
    {
        return await context.ReservationAttendees
            .AsNoTracking()
            .Include(attendee => attendee.User)
            .Where(attendee => attendee.ReservationId == reservationId)
            .OrderBy(attendee => attendee.CreatedAt)
            .ThenBy(attendee => attendee.Id)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsByReservationAndUserAsync(
        string reservationId,
        string userId,
        CancellationToken ct = default)
    {
        return context.ReservationAttendees
            .AsNoTracking()
            .AnyAsync(
                attendee =>
                    attendee.ReservationId == reservationId &&
                    attendee.UserId == userId,
                ct);
    }

    public Task<int> CountOccupyingByReservationAsync(
        string reservationId,
        CancellationToken ct = default)
    {
        return context.ReservationAttendees
            .AsNoTracking()
            .CountAsync(
                attendee =>
                    attendee.ReservationId == reservationId &&
                    attendee.Status != AttendeeStatusEnum.Declined,
                ct);
    }

    public async Task<Page<ReservationAttendeeModel>> Find(
        FilterReservationAttendeeDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        IQueryable<ReservationAttendeeModel> query =
            context.ReservationAttendees
                .AsNoTracking()
                .Include(x => x.Reservation)
                .Include(x => x.User)
                .Include(x => x.Inviter);

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
        // Reservation
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.ReservationId))
        {
            query = query.Where(x =>
                x.ReservationId == dto.ReservationId.Trim());
        }

        if (dto.ReservationStartsAtFrom.HasValue)
        {
            query = query.Where(x =>
                x.Reservation != null &&
                x.Reservation.StartsAt >= dto.ReservationStartsAtFrom.Value);
        }

        if (dto.ReservationStartsAtTo.HasValue)
        {
            query = query.Where(x =>
                x.Reservation != null &&
                x.Reservation.StartsAt <= dto.ReservationStartsAtTo.Value);
        }

        // ---------------------------------------------------------------
        // User
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.UserId))
        {
            query = query.Where(x =>
                x.UserId == dto.UserId.Trim());
        }

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

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            string pattern = $"%{dto.Email.Trim()}%";

            query = dto.EmailIgnoreCase == true
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
        // Role and status
        // ---------------------------------------------------------------

        if (dto.Role.HasValue)
            query = query.Where(x => x.Role == dto.Role.Value);

        if (dto.Status.HasValue)
            query = query.Where(x => x.Status == dto.Status.Value);

        // ---------------------------------------------------------------
        // Inviter
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.InvitedBy))
        {
            query = query.Where(x =>
                x.InvitedBy == dto.InvitedBy.Trim());
        }

        // ---------------------------------------------------------------
        // Response
        // ---------------------------------------------------------------

        if (dto.RespondedAtFrom.HasValue)
        {
            query = query.Where(x =>
                x.RespondedAt.HasValue &&
                x.RespondedAt.Value >= dto.RespondedAtFrom.Value);
        }

        if (dto.RespondedAtTo.HasValue)
        {
            query = query.Where(x =>
                x.RespondedAt.HasValue &&
                x.RespondedAt.Value <= dto.RespondedAtTo.Value);
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
        // Total count
        // ---------------------------------------------------------------

        long totalCount = await query.LongCountAsync(ct);

        // ---------------------------------------------------------------
        // Pagination
        // ---------------------------------------------------------------

        int pageNumber = Math.Max(dto.Page ?? 1, 1);
        int pageSize = Math.Clamp(dto.PageSize ?? 20, 1, 100);

        // ---------------------------------------------------------------
        // Deterministic ordering (por data da reserva, ideal p/ "meus convites")
        // ---------------------------------------------------------------

        query = dto.ColumnOrder == ColumnOrderEnum.DESC
            ? query
                .OrderByDescending(x => x.Reservation!.StartsAt)
                .ThenByDescending(x => x.Id)
            : query
                .OrderBy(x => x.Reservation!.StartsAt)
                .ThenBy(x => x.Id);

        long offset = ((long)pageNumber - 1) * pageSize;

        int skip = (int)Math.Min(offset, int.MaxValue);

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);

        return Page<ReservationAttendeeModel>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }
}