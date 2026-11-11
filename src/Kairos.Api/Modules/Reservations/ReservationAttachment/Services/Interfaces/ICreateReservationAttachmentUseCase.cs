using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Response;
using Kairos.Api.Modules.Reservations.ReservationAttachment.Models;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Interfaces;

public interface ICreateReservationAttachmentUseCase
{
    Task<Result<ReservationAttachmentModel>> ExecuteAsync(CreateReservationAttachmentCommand command, CancellationToken ct = default);
}