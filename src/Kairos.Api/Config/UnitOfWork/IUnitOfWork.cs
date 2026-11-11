using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.UserPreference.Repositories;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Reliability.Inbox.Repository;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Repositories;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Repositories;
using Kairos.Api.Modules.Rooms.Equipment.Repositories;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.RoomAttachment.Repositories;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomEquipment.Repositories;
using Kairos.Api.Modules.Rooms.RoomFavorite.Repositories;
using Kairos.Api.Modules.Rooms.RoomFeedback.Repositories;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kairos.Api.Config.UnitOfWork;

public interface IUnitOfWork
{
    IFileStorageService FileStorageService { get; }
    IRoomOperatingHourRepository RoomOperatingHourRepository { get; }
    IUserPreferenceRepository UserPreferenceRepository { get; }
    IReservationAttendeeRepository ReservationAttendeeRepository { get; }
    IEquipmentRepository EquipmentRepository { get; }
    IRoomEquipmentRepository RoomEquipmentRepository { get; }
    IUserRepository UserRepository { get; }
    IPermissionRepository PermissionRepository { get; }
    IPermissionUserRepository PermissionUserRepository { get; }
    ICategoryRepository CategoryRepository { get; }
    IRoomRepository RoomRepository { get; }
    IRoomTypeRepository RoomTypeRepository { get; }
    IRoomBlockoutRepository RoomBlockoutRepository { get; }
    IReservationRepository ReservationRepository { get; }
    IInboxRepository InboxRepository { get; }
    IRoomFavoriteRepository RoomFavoriteRepository { get; }
    IRoomFeedbackRepository RoomFeedbackRepository { get; }
    IReservationAttachmentRepository ReservationAttachmentRepository { get; }
    IRoomAttachmentRepository RoomAttachmentRepository { get; }

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
    
    Task<Result<T>> ExecuteTransactionWithInboxAsync<T>(
        string inboxId,
        string workerId,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken ct = default);
}