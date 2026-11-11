using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Providers;

public class DeleteReservationAttachmentUseCase(IUnitOfWork uow): IDeleteReservationAttachmentUseCase
{
    public async Task<Result<object>> ExecuteAsync(DeleteReservationAttachmentCommand command, CancellationToken ct = default)
    {
        ReservationAttachmentModel? reservation = await uow
            .ReservationAttachmentRepository
            .GetByIdAsync(command.AttachmentId, ct);
        
        if (reservation == null)
            return Result<object>.NotFound("The reservation was not found.");

        Result<bool> result = await uow
            .FileStorageService
            .DeleteAsync(StorageBucket.SupportMaterials, reservation.StorageKey, ct);
        
        if (result.IsFailure) 
            return Result<object>.Failure(result.Errors, result.StatusCode);

        await uow
            .ReservationAttachmentRepository
            .DeleteByIdAndCountAsync(command.AttachmentId, ct);
        
        return Result<object>.Ok(200);
    }
}