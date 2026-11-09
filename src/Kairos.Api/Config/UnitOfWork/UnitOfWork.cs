using System.Diagnostics;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.DB;
using Kairos.Api.Config.Observability;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Reliability.Inbox.Model;
using Kairos.Api.Modules.Reliability.Inbox.Repository;
using Kairos.Api.Modules.Reservations.Reservation.Repositories;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Repositories;
using Kairos.Api.Modules.Rooms.Equipment.Repositories;
using Kairos.Api.Modules.Rooms.Room.Repositories;
using Kairos.Api.Modules.Rooms.RoomBlockout.Repository;
using Kairos.Api.Modules.Rooms.RoomEquipment.Repositories;
using Kairos.Api.Modules.Rooms.RoomFavorite.Repositories;
using Kairos.Api.Modules.Rooms.RoomFeedback.Repositories;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Repository;
using Kairos.Api.Modules.Rooms.RoomTypes.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kairos.Api.Config.UnitOfWork;

public sealed class UnitOfWork(
    AppDbContext context,
    IRoomFavoriteRepository roomFavoriteRepository,
    IRoomFeedbackRepository roomFeedbackRepository,
    IUserRepository userRepository,
    IPermissionRepository permissionRepository,
    IPermissionUserRepository permissionUserRepository,
    ICategoryRepository categoryRepository,
    IRoomRepository roomRepository,
    IRoomTypeRepository roomTypeRepository,
    IRoomBlockoutRepository roomBlockoutRepository,
    IReservationRepository reservationRepository,
    IInboxRepository inboxRepository,
    IRoomOperatingHourRepository roomOperatingHourRepository,
    IEquipmentRepository equipmentRepository,
    IRoomEquipmentRepository roomEquipmentRepository,
    IReservationAttendeeRepository reservationAttendeeRepository
) : IUnitOfWork
{
    public IUserRepository UserRepository => userRepository;
    public IPermissionRepository PermissionRepository => permissionRepository;
    public IPermissionUserRepository PermissionUserRepository => permissionUserRepository;
    public ICategoryRepository CategoryRepository => categoryRepository;
    public IRoomRepository RoomRepository => roomRepository;
    public IRoomTypeRepository RoomTypeRepository => roomTypeRepository;
    public IRoomBlockoutRepository RoomBlockoutRepository => roomBlockoutRepository;
    public IReservationRepository ReservationRepository => reservationRepository;
    public IInboxRepository InboxRepository => inboxRepository;
    public IRoomOperatingHourRepository RoomOperatingHourRepository => roomOperatingHourRepository;
    public IEquipmentRepository EquipmentRepository => equipmentRepository;
    public IRoomEquipmentRepository RoomEquipmentRepository => roomEquipmentRepository;
    public IReservationAttendeeRepository ReservationAttendeeRepository => reservationAttendeeRepository;
    public IRoomFavoriteRepository RoomFavoriteRepository => roomFavoriteRepository;
    public IRoomFeedbackRepository RoomFeedbackRepository => roomFeedbackRepository;

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

    public async Task<Result<T>> ExecuteTransactionWithInboxAsync<T>(
        string inboxId,
        string workerId,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inboxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ArgumentNullException.ThrowIfNull(operation);

        if (context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "ExecuteTransactionWithInboxAsync must be called outside an existing transaction.");
        }

        using Activity? activity = Telemetry.ActivitySource.StartActivity("inbox.transaction");

        activity?.SetTag("kairos.inbox.id", inboxId);
        activity?.SetTag("kairos.inbox.worker_id", workerId);

        try
        {
            bool alreadyExists = await inboxRepository.ExistsByMessageIdAsync(inboxId, ct);

            if (alreadyExists)
            {
                activity?.SetTag("kairos.inbox.outcome", "in_processing");
                Telemetry.InboxConflicts.Add(1);

                return Result<T>.Conflict("Request in processing.");
            }

            Result<T> outcome = await ExecuteTransactionAsync(
                async token =>
                {
                    InboxModel inbox = new InboxModel()
                    {
                        Id = Guid.NewGuid().ToString(),
                        MessageId = inboxId,
                        MessageType = "application/json",
                        Source = workerId,
                        Payload = "{\"data\": 1}",
                        Status = InboxStatusEnum.Processing,
                        CreatedAt = DateTimeOffset.UtcNow
                    };

                    await inboxRepository.AddAsync(inbox, token);

                    var result = await operation(token);

                    if (result.IsFailure)
                        return result;

                    inbox.Status = InboxStatusEnum.Processed;
                    inbox.UpdatedAt = DateTimeOffset.UtcNow;

                    return result;
                },
                ct);

            activity?.SetTag("kairos.inbox.outcome", outcome.IsSuccess ? "processed" : "failed");
            activity?.SetTag("kairos.result.status_code", outcome.StatusCode);

            if (outcome.StatusCode >= 500)
            {
                activity?.SetStatus(ActivityStatusCode.Error, outcome.FirstError);
            }

            return outcome;
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}