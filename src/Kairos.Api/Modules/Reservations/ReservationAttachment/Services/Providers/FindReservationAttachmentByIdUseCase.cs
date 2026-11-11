using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Response;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Mapper;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Providers;

public sealed class FindReservationAttachmentByIdUseCase(
    IUnitOfWork uow)
    : IFindReservationAttachmentByIdUseCase
{
    public async Task<Result<ReservationAttachmentDto>> ExecuteAsync(
        FindReservationAttachmentByIdCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        ReservationAttachmentModel? attachment =
            await uow.ReservationAttachmentRepository
                .GetByIdWithDetailsAsync(command.AttachmentId, ct);

        if (attachment is null)
        {
            return Result<ReservationAttachmentDto>.NotFound(
                "The reservation attachment was not found.");
        }
        
        Result<PresignedUrl> downloadResult =
            uow.FileStorageService.GetDownloadUrl(
                StorageBucket.SupportMaterials,
                attachment.StorageKey);

        if (downloadResult.IsFailure)
        {
            return Result<ReservationAttachmentDto>.Failure(
                downloadResult.Errors,
                downloadResult.StatusCode);
        }

        string downloadUrl = downloadResult.Value!.Url;

        ReservationAttachmentDto dto =
            ReservationAttachmentMapper.ToDto(
                attachment,
                downloadUrl);

        return Result<ReservationAttachmentDto>.Success(dto);
    }
}