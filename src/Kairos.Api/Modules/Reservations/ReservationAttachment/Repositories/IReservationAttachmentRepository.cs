using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Repositories;

public interface IReservationAttachmentRepository
    : IBaseRepository<ReservationAttachmentModel>
{
    Task<ReservationAttachmentModel?> GetByIdWithDetailsAsync(
        string attachmentId,
        CancellationToken ct = default);

    Task<Page<ReservationAttachmentModel>> Find(
        FilterReservationAttachmentDto dto,
        CancellationToken ct = default);
    
    Task<ReservationAttachmentModel?> GetByStorageKeyAsync(
        string storageKey,
        CancellationToken ct = default);
}