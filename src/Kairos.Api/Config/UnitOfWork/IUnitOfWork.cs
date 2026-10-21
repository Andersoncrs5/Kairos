using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kairos.Api.Config.UnitOfWork;

public interface IUnitOfWork
{
    IUserRepository UserRepository { get; }

    IPermissionRepository PermissionRepository { get; }

    IPermissionUserRepository PermissionUserRepository { get; }

    ICategoryRepository CategoryRepository { get; }

    IRoomRepository RoomRepository { get; }

    IRoomTypeRepository RoomTypeRepository { get; }

    IRoomBlockoutRepository RoomBlockoutRepository { get; }

    IReservationRepository ReservationRepository { get; }

    Task<int> CommitAsync(
        CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);

    Task ExecuteTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
    
    Task<Result<T>> ExecuteTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken ct = default);
}