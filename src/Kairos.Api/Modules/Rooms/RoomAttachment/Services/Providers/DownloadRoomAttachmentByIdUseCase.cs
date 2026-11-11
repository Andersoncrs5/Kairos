using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Kairos.Api.Modules.Rooms.RoomAttachment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Services.Providers;

public sealed class DownloadRoomAttachmentByIdUseCase(
    IUnitOfWork uow)
    : IDownloadRoomAttachmentByIdUseCase
{
    public async Task<Result<FileDownload>> ExecuteAsync(
        DownloadRoomAttachmentByIdCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        RoomAttachmentModel? attachment = await uow
            .RoomAttachmentRepository
            .GetByIdAsync(command.AttachmentId, ct);

        if (attachment is null)
            return Result<FileDownload>.NotFound("The room attachment was not found.");

        return await uow.FileStorageService.DownloadAsync(
            StorageBucket.RoomMedia,
            attachment.StorageKey,
            ct);
    }
}