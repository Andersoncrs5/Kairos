using Kairos.Api.Common.Result;
using Kairos.Api.Config.DB;
using Kairos.Api.Modules.Categories.Repositories;
using Kairos.Api.Modules.Identity.Permissions.Repositories;
using Kairos.Api.Modules.Identity.PermissionUsers.Repositories;
using Kairos.Api.Modules.Identity.Users.Repositories;
using Kairos.Api.Modules.Reliability.Inbox.Repository;
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
    IReservationRepository reservationRepository,
    IInboxRepository inboxRepository
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

        bool acquired = await inboxRepository.TryMarkAsProcessingAsync(
            inboxId,
            workerId,
            ct);

        if (!acquired)
        {
            return Result<T>.Conflict(
                "Inbox is not available for processing.");
        }

        return await ExecuteTransactionAsync(
            async token =>
            {
                var result = await operation(token);

                if (result.IsFailure)
                    return result;

                var processed = await inboxRepository.TryMarkAsProcessedAsync(
                    inboxId,
                    workerId,
                    token);

                if (!processed)
                {
                    return Result<T>.Conflict(
                        "Could not complete Inbox processing.");
                }

                return result;
            },
            ct);
    }
}

/*
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
           throw new InvalidOperationException(
               "ExecuteTransactionWithInboxAsync must be called outside an existing transaction.");
   
       bool claimed = false;
       var strategy = context.Database.CreateExecutionStrategy();
   
       try
       {
           var result = await strategy.ExecuteAsync(async () =>
           {
               claimed = false;
               context.ChangeTracker.Clear();
   
               await using var tx = await context.Database.BeginTransactionAsync(ct);
   
               if (!await inboxRepository.TryMarkAsProcessingAsync(inboxId, workerId, ct))
                   return Result<T>.Conflict("Inbox is not available for processing.");
   
               claimed = true;
   
               var result = await operation(ct);
   
               if (result.IsFailure)
               {
                   await tx.RollbackAsync(CancellationToken.None);
                   return result;
               }
   
               await context.SaveChangesAsync(ct);
   
               if (!await inboxRepository.TryMarkAsProcessedAsync(inboxId, workerId, ct))
               {
                   await tx.RollbackAsync(CancellationToken.None);
                   return Result<T>.Conflict("Could not complete Inbox processing.");
               }
   
               await tx.CommitAsync(ct);
               return result;
           });
   
           if (claimed && result.IsFailure)
           {
               context.ChangeTracker.Clear();
               await inboxRepository.RegisterInboxFailureAsync(
                   inboxId, result.FirstError, permanent: result.StatusCode < 500, ct);
           }
   
           return result;
       }
       catch (Exception ex) when (claimed)
       {
           context.ChangeTracker.Clear();
           await inboxRepository.RegisterInboxFailureAsync(
               inboxId, ex.Message, permanent: false, CancellationToken.None);
           throw;
       }
   }
   */