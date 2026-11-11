using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Response;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Interfaces;

public interface IFindReservationAttachmentByIdUseCase
    : IUseCase<
        FindReservationAttachmentByIdCommand,
        Result<ReservationAttachmentDto>>
{
}