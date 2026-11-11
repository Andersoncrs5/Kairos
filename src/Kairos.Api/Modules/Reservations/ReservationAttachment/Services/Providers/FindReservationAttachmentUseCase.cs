using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Providers;

public sealed class FindReservationAttachmentUseCase(
    IUnitOfWork uow)
    : IFindReservationAttachmentUseCase
{
    public async Task<Result<Page<ReservationAttachmentModel>>> ExecuteAsync(
        FilterReservationAttachmentsCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Dto);

        Page<ReservationAttachmentModel> attachments =
            await uow.ReservationAttachmentRepository
                .Find(command.Dto, ct);
        
        return Result<Page<ReservationAttachmentModel>>.Success(attachments);
    }
}