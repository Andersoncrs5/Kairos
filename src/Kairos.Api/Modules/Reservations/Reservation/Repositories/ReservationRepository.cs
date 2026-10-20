using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Enums;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Microsoft.EntityFrameworkCore;

namespace Kairos.Api.Modules.Reservations.Reservation.Repositories;

public sealed class ReservationRepository(AppDbContext context)
    : BaseRepositoryImpl<ReservationModel>(context),
        IReservationRepository
{
    public Task<ReservationModel?> GetByIdempotencyKeyAsync(
        string userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return context.Set<ReservationModel>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                reservation =>
                    reservation.UserId == userId &&
                    reservation.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ReservationModel>> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<ReservationModel>()
            .AsNoTracking()
            .Where(reservation => reservation.UserId == userId)
            .OrderByDescending(reservation => reservation.StartsAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReservationModel>> GetConflictingByRoomAsync(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<ReservationModel>()
            .AsNoTracking()
            .Where(reservation =>
                reservation.RoomId == roomId &&
                reservation.Status != ReservationStatusEnum.Cancelled &&
                reservation.StartsAt < endsAt &&
                reservation.EndsAt > startsAt)
            .OrderBy(reservation => reservation.StartsAt)
            .ToListAsync(cancellationToken);
    }
}