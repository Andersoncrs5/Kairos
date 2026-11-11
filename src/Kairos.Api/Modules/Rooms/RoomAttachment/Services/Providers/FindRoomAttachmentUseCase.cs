using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;
using Kairos.Api.Modules.Rooms.RoomAttachment.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Services.Providers;

public sealed class FindRoomAttachmentUseCase(
    IUnitOfWork uow)
    : IFindRoomAttachmentUseCase
{
    public async Task<Result<Page<RoomAttachmentModel>>> ExecuteAsync(
        FilterRoomAttachmentsCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Dto);

        Page<RoomAttachmentModel> attachments = await uow
            .RoomAttachmentRepository
            .Find(command.Dto, ct);

        return Result<Page<RoomAttachmentModel>>.Success(attachments);
    }
}