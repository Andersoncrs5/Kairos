using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Reservations.ReservationAttachment.DTOs.Commands;

namespace Kairos.Api.Modules.Reservations.ReservationAttachment.Services.Interfaces;

public interface IDeleteReservationAttachmentUseCase: IUseCase<DeleteReservationAttachmentCommand, Result<object>> { }