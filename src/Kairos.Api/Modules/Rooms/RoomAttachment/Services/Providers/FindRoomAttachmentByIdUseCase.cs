using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomAttachment.Mappers;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Kairos.Api.Modules.Rooms.RoomAttachment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Services.Providers;

public sealed class FindRoomAttachmentByIdUseCase(
    IUnitOfWork uow)
    : IFindRoomAttachmentByIdUseCase
{
    public async Task<Result<RoomAttachmentModel>> ExecuteAsync(
        FindRoomAttachmentByIdCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        RoomAttachmentModel? attachment = await uow
            .RoomAttachmentRepository
            .GetByIdWithDetailsAsync(command.AttachmentId, ct);

        if (attachment is null)
        {
            return Result<RoomAttachmentModel>.NotFound(
                "The room attachment was not found.");
        }
        
        return Result<RoomAttachmentModel>.Success(attachment);
    }
}