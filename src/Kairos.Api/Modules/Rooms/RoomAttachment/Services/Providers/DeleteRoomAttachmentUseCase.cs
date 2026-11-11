using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Kairos.Api.Modules.Rooms.RoomAttachment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Services.Providers;

public sealed class DeleteRoomAttachmentUseCase(IUnitOfWork uow) : IDeleteRoomAttachmentUseCase
{
    public async Task<Result<object>> ExecuteAsync(
        DeleteRoomAttachmentCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        RoomAttachmentModel? attachment = await uow
            .RoomAttachmentRepository
            .GetByIdAsync(command.AttachmentId, ct);

        if (attachment is null)
        {
            return Result<object>.NotFound(
                "The room attachment was not found.");
        }

        Result<bool> result = await uow
            .FileStorageService
            .DeleteAsync(
                StorageBucket.RoomMedia,
                attachment.StorageKey,
                ct);

        if (result.IsFailure)
        {
            return Result<object>.Failure(
                result.Errors,
                result.StatusCode);
        }

        await uow
            .RoomAttachmentRepository
            .DeleteByIdAndCountAsync(
                command.AttachmentId,
                ct);

        return Result<object>.Ok(200);
    }
}