using System.Diagnostics;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.DB;
using Kairos.Api.Config.Observability;
using Kairos.Api.Config.Resilience; // NOVO
using Kairos.Api.Config.Storage;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.UserPreference.Repositories;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Reliability.Inbox.Model;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Polly; // NOVO
using Polly.Registry; // NOVO

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
    IReservationAttendeeRepository reservationAttendeeRepository,
    IUserPreferenceRepository userPreferenceRepository,
    IReservationAttachmentRepository reservationAttachmentRepository,
    IRoomAttachmentRepository roomAttachmentRepository,
    IFileStorageService fileStorageService,
    ResiliencePipelineProvider<string> pipelines // NOVO
) : IUnitOfWork
{
    // NOVO
    private readonly ResiliencePipeline _pipeline =
        pipelines.GetPipeline(ResiliencePipelines.Database);

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
    public IUserPreferenceRepository UserPreferenceRepository => userPreferenceRepository;
    public IFileStorageService FileStorageService => fileStorageService;
    public IReservationAttachmentRepository ReservationAttachmentRepository => reservationAttachmentRepository;
    public IRoomAttachmentRepository RoomAttachmentRepository => roomAttachmentRepository;

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

    // NOVO: strategy do EF -> pipeline do Polly
    public async Task ExecuteTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await _pipeline.ExecuteAsync(async token =>
        {
            await using var transaction =
                await context.Database.BeginTransactionAsync(token);

            try
            {
                await operation(token);

                await context.SaveChangesAsync(token);

                await transaction.CommitAsync(token);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);

                // A próxima tentativa não pode enxergar entidades rastreadas pela que falhou
                context.ChangeTracker.Clear();

                throw;
            }
        }, cancellationToken);
    }

    // NOVO: strategy do EF -> pipeline do Polly
    public async Task<Result<T>> ExecuteTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (context.Database.CurrentTransaction is not null)
            return await operation(ct);

        return await _pipeline.ExecuteAsync<Result<T>>(async token =>
        {
            await using var tx = await context.Database.BeginTransactionAsync(token);

            try
            {
                var result = await operation(token);

                if (!result.IsSuccess)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    context.ChangeTracker.Clear();
                    return result;
                }

                await context.SaveChangesAsync(token);
                await tx.CommitAsync(token);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                throw;
            }
        }, ct);
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