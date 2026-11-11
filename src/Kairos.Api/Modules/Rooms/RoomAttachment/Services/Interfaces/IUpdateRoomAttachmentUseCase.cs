using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Services.Interfaces;

public interface IUpdateRoomAttachmentUseCase
    : IUseCase<
        UpdateRoomAttachmentCommand,
        Result<RoomAttachmentModel>>
{
}