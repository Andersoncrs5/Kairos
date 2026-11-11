using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Rooms.RoomAttachment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomAttachment.Models;

namespace Kairos.Api.Modules.Rooms.RoomAttachment.Repositories;

public interface IRoomAttachmentRepository
    : IBaseRepository<RoomAttachmentModel>
{
    Task<RoomAttachmentModel?> GetByIdWithDetailsAsync(
        string attachmentId,
        CancellationToken ct = default);

    Task<Page<RoomAttachmentModel>> Find(
        FilterRoomAttachmentDto dto,
        CancellationToken ct = default);

    Task<RoomAttachmentModel?> GetByStorageKeyAsync(
        string storageKey,
        CancellationToken ct = default);
}