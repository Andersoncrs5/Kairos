using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Commands;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Services.Interfaces;

public interface IDownloadRoomAttachmentByStorageKeyUseCase
    : IUseCase<
        DownloadRoomAttachmentByStorageKeyCommand,
        Result<FileDownload>>
{
}