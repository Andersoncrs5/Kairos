using Kairos.Api.Common.Result;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kairos.Api.Config.UnitOfWork;

public sealed class UnitOfWork(
    AppDbContext context,
    IUserRepository userRepository,
    IPermissionRepository permissionRepository,
    IPermissionUserRepository permissionUserRepository,
    ICategoryRepository categoryRepository,
    IRoomRepository roomRepository,
    IRoomTypeRepository roomTypeRepository,
    IRoomBlockoutRepository roomBlockoutRepository,
    IReservationRepository reservationRepository
) : IUnitOfWork
{
    public IUserRepository UserRepository => userRepository;

    public IPermissionRepository PermissionRepository =>
        permissionRepository;

    public IPermissionUserRepository PermissionUserRepository =>
        permissionUserRepository;

    public ICategoryRepository CategoryRepository =>
        categoryRepository;

    public IRoomRepository RoomRepository => roomRepository;

    public IRoomTypeRepository RoomTypeRepository =>
        roomTypeRepository;

    public IRoomBlockoutRepository RoomBlockoutRepository =>
        roomBlockoutRepository;

    public IReservationRepository ReservationRepository =>
        reservationRepository;

    public Task<int> CommitAsync(
        CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        return context.Database.BeginTransactionAsync(
            cancellationToken);
    }

    public async Task ExecuteTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await context.Database.BeginTransactionAsync(
                    cancellationToken);

            try
            {
                await operation(cancellationToken);

                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);

                throw;
            }
        });
    }
    
    public async Task<Result<T>> ExecuteTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (context.Database.CurrentTransaction is not null)
            return await operation(ct);

        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await context.Database.BeginTransactionAsync(ct);

            try
            {
                var result = await operation(ct);

                if (!result.IsSuccess)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    context.ChangeTracker.Clear();
                    return result;
                }

                await context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }
}