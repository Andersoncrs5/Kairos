using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Providers;

public sealed class DownloadReservationAttachmentByIdUseCase(
    IUnitOfWork uow)
    : IDownloadReservationAttachmentByIdUseCase
{
    public async Task<Result<FileDownload>> ExecuteAsync(
        DownloadReservationAttachmentByIdCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        ReservationAttachmentModel? attachment =
            await uow.ReservationAttachmentRepository
                .GetByIdAsync(command.AttachmentId, ct);

        if (attachment is null)
            return Result<FileDownload>.NotFound("The reservation attachment was not found.");
        
        return await uow.FileStorageService.DownloadAsync(StorageBucket.SupportMaterials, attachment.StorageKey, ct);
    }
}